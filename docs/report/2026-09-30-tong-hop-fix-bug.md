[TỔNG HỢP] Fix 14 bug từ đợt rà code CineBook ngày 2026-09-30 (1 mục bị loại vì không phải bug)

Ticket: [DEV ĐIỀN]
Branch / commit: main / chưa commit — base BE 58fcdf3 (2026-09-27), base FE (repo web/) 5200ea7 (2026-09-27)
Người fix / ngày: [DEV ĐIỀN] / 2026-09-30
Liên quan: 2026-09-30-giu-ghe-cho-thanh-toan.md (bug giữ ghế, fix trước đó, cùng đợt)

0. ĐỐI CHIẾU TÀI LIỆU KHÁCH
- Không có tài liệu nghiệp vụ gốc trong workspace: README.md:5 trỏ tới ../danh-gia-nghiep-vu-btl/, thư mục không tồn tại. Luật BR-xx chỉ còn được trích lại trong comment code và README.
- Các luật có trích dẫn:
  - BR-07/BR-08: PaymentService.cs:12-15 "nguồn sự thật duy nhất là webhook".
  - BR-14: RefundService.cs:35.
  - BR-15: ShowtimeService.cs "hủy suất chiếu thì mọi vé đã bán được hoàn 100%" (comment ngay trên CancelAsync).
  - BR-19: ApiExtensions.cs:14 "nhân viên và quản lý rạp bị giới hạn trong rạp mình phụ trách".
  - Tính năng hủy vé và trailer ở trang quản trị: README.md:244 và 320.
Kết luận: (B) cho #7, #11 (code có ghi luật nhưng làm thiếu) và #6, #13 (README mô tả tính năng nhưng giao diện thiếu). Các mục còn lại là lỗi kỹ thuật, không phụ thuộc spec.

1-2. NGUYÊN NHÂN VÀ CÁCH SỬA (dòng "trước" là file ở base commit, dòng "sau" là file hiện tại)

#2 Tiền đã trừ nhưng không có vé, không hoàn
- Trước: PaymentService.cs:138-145. Khi ConfirmPaidAsync thất bại (ghế đã nhả, đơn đã đóng, đơn đã được giao dịch khác thanh toán), code đặt payment = Succeeded rồi trả lỗi, không hoàn tiền. VnPayGateway.cs:144: link VNPay sống 15 phút trong khi ghế chỉ giữ 8 phút.
- Sửa:
  - PaymentService.HandleCallbackAsync: không chốt được đơn thì gọi AutoRefundAsync, hoàn 100% qua cổng. Payment chuyển Refunded; đơn chuyển Refunded kèm CancelReason, trừ trường hợp trả trùng thì đơn Paid giữ nguyên. Ghi audit AUTO_REFUND. Cổng từ chối hoàn thì ghi REFUND_REQUIRED để xử lý tay.
  - Thêm nhận diện trả trùng: đơn đã có một payment Succeeded khác.
  - VnPayGateway.CreateAsync: vnp_ExpireDate lấy mốc sớm hơn giữa "tạo + 15 phút" và "hết giữ ghế".

#3 Webhook không đối chiếu số tiền
- Trước: PaymentService.cs:127-139 không so callback.Amount với payment.Amount.
- Sửa:
  - Lệch tiền thì payment chuyển Failed, trả AMOUNT_MISMATCH; IPN VNPay trả RspCode 04.
  - Cổng giả lập gửi kèm amount; gọi thẳng không kèm amount (tests/test_business_flow.py) thì tự điền đúng số tiền giao dịch.
  - MockPaymentGateway parse amount bằng InvariantCulture.

#5 XSS và chuyển hướng tuỳ ý ở /mock-gateway
- Trước: PaymentsController.cs:158-159 chèn thô {{code}}, {{txn}} vào HTML. Dòng 167 và 178 gán returnUrl thẳng vào location.href, nhận cả "javascript:…" và tên miền lạ. Trang này cùng nguồn với app, token nằm trong localStorage.
- Sửa:
  - HtmlEncode code, txn, amount.
  - returnUrl chỉ nhận đường dẫn tương đối hoặc cùng host (IsSameOrigin).
  - FE Booking.tsx gửi returnUrl tương đối, vì khi deploy qua rewrite của Vercel thì API không biết tên miền giao diện.

#10 Xem được đơn của người khác
- Trước: BookingsController.cs:28 gọi MapAsync(id) không kèm UserId.
- Sửa: thêm BookingService.GetForUserAsync(id, userId). Đơn không phải của mình trả 404.

#8 Hủy vé mất vé nhưng không được hoàn tiền
- Trước: RefundService.cs:47-60. Code đổi đơn sang Cancelled, nhả ghế và SaveChanges (dòng 54) trước khi gọi cổng hoàn (dòng 56). Cổng từ chối thì đơn đã hủy, tiền không về.
- Sửa: bọc cả luồng trong transaction; cổng từ chối thì rollback (đơn Paid, vé Valid, ghế Sold như cũ).
- Cùng file BookingOptions.cs:24: RefundFullBeforeHours khai báo dạng field nên cấu hình không đọc được → đổi thành property.

#6 Không có nút hủy vé trên giao diện
- Trước: MyTickets.tsx:50 chỉ có thao tác xem QR; README.md:244 ghi có hủy vé.
- Sửa: nút "Hủy vé" cho đơn Paid còn trong hạn hoàn. Hộp xác nhận báo trước tỉ lệ và số tiền hoàn; xong thì báo số tiền thật do máy chủ trả.

#9 Đơn đã hủy hoặc hoàn tiền mất danh sách ghế
- Trước: BookingService.cs:320 và 345 đặt "BookingId" = NULL khi nhả ghế (dòng ShowtimeSeat còn bán lại), nên BookedSeats của đơn rỗng.
- Sửa:
  - BookingService.ToDtosAsync: đơn không còn BookedSeats thì dựng ghế từ Tickets. Giá lấy lại từ ShowtimeSeat của đúng suất và đúng ghế (giá đã chốt, không đổi).
  - "Vé của tôi" ẩn đơn khách tự hủy khi chưa trả tiền (PaidAtUtc null).
  - BookingDto thêm CancelReason.

#7 Hủy suất chiếu không hoàn tiền
- Trước: ShowtimeService.cs:179-208 chỉ đổi đơn Paid sang Cancelled; không hoàn tiền, không hủy vé, bỏ qua đơn Pending; hủy lần hai vẫn chạy lại.
- Sửa:
  - Đơn Paid: vé chuyển Cancelled, gọi PaymentService.RefundAsync 100%, đơn chuyển Refunded. Hoàn lỗi thì đơn chuyển Cancelled kèm ghi chú "cần xử lý thủ công".
  - Đơn Pending chuyển Cancelled; tiền về sau đó thì luồng #2 tự hoàn.
  - Chặn hủy suất đã hủy. Audit ghi số đơn hoàn được / tổng.

#11 Quản lý rạp và nhân viên không bị giới hạn theo rạp (BR-19)
- Trước:
  - ShowtimesController.cs:34, 47, 53 và CatalogController (tạo phòng, báo ghế hỏng) không truyền rạp của người gọi.
  - ReportsController.cs:14: tài khoản quản lý thiếu cinema_id thì Scope() = null, tức xem được toàn hệ thống.
- Sửa:
  - ApiExtensions.ManagedCinemaId(): Admin trả null; người khác trả rạp của mình, thiếu rạp thì Guid.Empty (không khớp rạp nào).
  - Áp vào tạo, mở/ngừng bán, hủy suất chiếu, danh sách suất admin, tạo phòng, báo ghế hỏng, báo cáo. Sai rạp trả 403 FORBIDDEN_CINEMA.
  - FE Showtimes.tsx và Rooms.tsx: quản lý rạp chỉ thấy rạp mình trong bộ chọn.

#12 Suất chiếu biến mất khỏi trang quản trị
- Trước:
  - Showtimes.tsx:25-26 (FE) dùng /api/showtimes của khách. API này lọc IsOnSale và bỏ suất sắp chiếu hoặc đã qua (ShowtimeService.cs:148 và 163), nên bấm "Ngừng bán" là suất mất khỏi danh sách và không mở bán lại được.
  - Showtimes.tsx:21 chỉ lấy phim NowShowing.
- Sửa:
  - Thêm GET /api/showtimes/admin (SearchForAdminAsync): gồm cả suất ngừng bán, đã qua giờ, đã hủy; DTO thêm IsCancelled.
  - FE hiện badge "Đã hủy" và ẩn thao tác với suất đã hủy.
  - Cho tạo suất cho phim sắp chiếu.

#13 Sửa phim làm mất dữ liệu
- Trước:
  - Movies.tsx:43 (FE) luôn gửi genreIds: []; MoviesController.cs:83 luôn RemoveRange thể loại → mỗi lần sửa là mất thể loại.
  - Movies.tsx:67 và 69 ghi đè endDate, country thành rỗng.
  - Movies.tsx:30 và MoviesController.cs:23: phim Draft không hiện ở trang quản trị.
  - Form không có ô trailer, dù README.md:320 ghi có.
- Sửa:
  - BE chỉ thay thể loại khi GenreIds khác null.
  - MovieDetailDto thêm Country, EndDate, GenreIds.
  - /api/movies?all=true cho Admin/Manager thấy phim nháp.
  - Form có chọn thể loại (fieldset, không lồng label), quốc gia, ngày kết thúc, diễn viên, ảnh nền, trailer.

#14 Không hỏi xác nhận khi hủy suất hoặc xoá phim
- Trước: Showtimes.tsx:118 và Movies.tsx:125 gọi mutate ngay. Lý do hủy bị hard code "Hủy do sự cố kỹ thuật" (Showtimes.tsx:31).
- Sửa: hủy suất hỏi xác nhận, nêu số ghế bị ảnh hưởng và bắt nhập lý do (khách sẽ thấy). Xoá phim hỏi xác nhận. Cả hai hiện kết quả trả về.

#16 Số liệu báo cáo lệch nhau
- Trước:
  - ReportService.cs:20: ô doanh thu cộng cả đơn Refunded với đủ Total, trong khi biểu đồ theo ngày chỉ tính Paid.
  - Dòng 33: vé hôm nay không lọc theo rạp, không bỏ vé đã hủy.
  - Dòng 92: doanh thu theo phim cộng giá niêm yết của ghế, bỏ qua giảm giá.
- Sửa: cả ba chỉ tính đơn Paid; vé đếm theo đúng tập đơn đó; doanh thu phim = SeatsSubtotal − DiscountAmount. Khoản khách tự hủy mất 20% (hoàn một phần) không còn tính vào doanh thu — cần xác nhận nghiệp vụ.

Lặt vặt (sửa cùng đợt)
- api.ts:38 (FE): nhận 401 thì chỉ xoá token, giao diện vẫn hiện như đang đăng nhập. Nay phát sự kiện, AuthProvider đưa user về null.
- BookingResult.tsx:23: API lỗi thì quay spinner mãi → nay hiện "Không xem được đơn hàng". Đơn Refunded thì hiện tiêu đề "Đã hoàn tiền" kèm lý do.
- Auth.tsx: nhân viên và quản trị đăng nhập từ trang bị chặn nay quay về đúng trang đó (tham số next).
- TicketService.cs:21: bỏ ContinueWith (lỗi bị gói thành AggregateException).
- RoomService.cs:85: sức chứa phòng chỉ đếm ghế đang hoạt động. Rooms.tsx:94: số ghế dự kiến tính cả hàng ghế đôi chỉ có nửa số ghế.

KHÔNG PHẢI BUG — ĐÃ LOẠI
#15 "Soát vé báo VÉ ĐÃ DÙNG do camera gửi mã nhiều lần": nhận định sai từ lúc đọc code.
- html5-qrcode 2.3.8: vòng quét tuần tự. stop() đặt shouldScan = false đồng bộ (esm/html5-qrcode.js:231), vòng kế thoát ở dòng 555. Callback của app gọi stop() trong cùng tick.
- Chạy thật bằng camera giả phát mã QR của vé thật trên bản FE gốc: 1 yêu cầu /api/checkin/scan, kết quả CHO VÀO.
- Đã hoàn tác thay đổi ở CheckIn.tsx.

3. CHECK LỖI TƯƠNG TỰ
- Grep "MapAsync(" trong src: còn BookingService (lúc giữ ghế, của chính người gọi) → an toàn. Grep "UserId == userId" ở các endpoint /api/bookings/{id}/*: tickets, refund, cancel, payments đều đã lọc theo chủ đơn.
- Grep "Content(html|{{" trong CineBook.Api: chỉ trang mock-gateway (PaymentsController.cs:201) dựng HTML thô; 3 giá trị chèn vào HTML (dòng 171-173) đều đã encode.
- Grep "CinemaId" trong Controllers: CheckInController đã lọc rạp từ trước (CheckInService.cs:58). Chưa lọc: MoviesController (danh mục phim dùng chung toàn hệ thống) — xem mục CÒN LẠI.
- Grep "ReleaseSeatsAsync|ReleaseSeatsOfBookingAsync": 4 nơi gọi (CancelPending, CloseOwnPending, RefundService, AutoRefund) — đều hiển thị ghế qua ToDtosAsync.

4. PHẠM VI ẢNH HƯỞNG
- API đổi hành vi:
  - GET /api/bookings/{id}: 404 với đơn của người khác.
  - Webhook: tự hoàn tiền, AMOUNT_MISMATCH.
  - /refund: rollback khi cổng từ chối.
  - POST /api/showtimes/{id}/cancel: hoàn tiền; lần hai trả 400.
  - Các API quản trị theo rạp: có thể trả 403.
  - GET /api/movies/{id}: thêm field. /api/reports/*: số liệu thay đổi theo định nghĩa mới.
- API thêm mới: GET /api/showtimes/admin; /api/movies?all=true.
- Màn hình cần regression: Đặt vé (thanh toán), Kết quả thanh toán, Vé của tôi, Soát vé, Admin (Tổng quan, Phim, Suất chiếu, Rạp & phòng, Báo cáo), trang cổng giả lập.
- Không đổi schema, không cần migration. README đã cập nhật ở lượt trước.

5. LÝ DO LỌT BUG — 5WHY (Dev)
Why1: Vì sao 14 lỗi này tồn tại cùng lúc mà test vẫn "28 đạt · 0 lỗi"? → tests/test_business_flow.py có 14 nhóm kiểm tra (đăng nhập, tạo suất, chồng giờ, sơ đồ ghế, giữ ghế, giành ghế, giới hạn ghế, combo/voucher, thanh toán, webhook trùng, vé QR, soát vé, báo cáo có số). Không nhóm nào chạm tới hủy/hoàn vé, hủy suất, quyền theo rạp, xem đơn người khác, hay trang quản trị.
Why2: Vì sao các nhóm đó không có? → Bộ test đi theo đường vui (mua được vé, soát được vé) và các luật "chống" đã nêu trong README (BR-01, BR-08, BR-11, BR-12). Các luật "đền bù" (BR-14, BR-15) và phân quyền (BR-19) chỉ được viết thành comment trong code, không có kịch bản kiểm.
Why3: Vì sao lỗi ở giao diện quản trị (#12, #13, #14) không bị phát hiện? → Không có kiểm thử nào cho FE. Trang admin gọi API của khách (#12) và gửi genreIds cố định (#13), chỉ lộ ra khi thao tác đủ một vòng sửa và lưu.
Root cause chính: Thiếu test case — cần thêm vào tests/test_business_flow.py các nhóm: hủy vé + hoàn (kể cả cổng từ chối), hủy suất chiếu, thanh toán muộn/trùng, lệch số tiền, quyền theo rạp, xem đơn người khác. Có thể chép kịch bản từ bộ kiểm chứng của đợt này.
Jira > Cause Category: IMPLEMENT_Insufficient analysis before implementation
Jira > Defect Type: #2 #3 #7 #8 Fun_Wrong business logic · #5 #10 #11 Cod_Logic of Code (bảo mật/phân quyền) · #6 Fun_Feature Missing · #9 #12 #13 #16 Fun_Incomplete Function · #14 UI _Label/Message

KIỂM CHỨNG ĐÃ CHẠY (Postgres 16 local trong docker, không dùng Neon; đã xoá sau khi chạy)
- Script API: kiểm từng mục #2 #3 #5 #7 #8 #9 #10 #11 #12 #13 #16 và cấu hình RefundFullBeforeHours=1000 (hoàn đúng 80%) — 49/50 PASS.
  - Mục fail duy nhất là phép kiểm "mọi đơn Paid của suất đều hoàn đủ" ở lần chạy thứ hai: suất đó chứa một đơn mà lần chạy đầu đã cố ý đổi cổng thành không tồn tại.
  - Hệ thống xử lý đúng nhánh lỗi: đơn chuyển Cancelled, ghi "cần xử lý thủ công", audit "hoàn tiền 12/13 đơn".
- Test có sẵn: test_business_flow.py 28/28; test_race_condition.py 20 luồng → đúng 1 người giữ được ghế.
- E2E trình duyệt (Chromium), 15/15 PASS:
  - Hủy vé ở Vé của tôi: có số tiền hoàn, vẫn hiện ghế.
  - Trang kết quả với đơn người khác; 401 thì header đăng xuất.
  - Admin Suất chiếu: ngừng/mở bán, hủy có hỏi lý do, badge Đã hủy.
  - Bộ lọc rạp của quản lý.
  - Admin Phim: tích sẵn thể loại, giữ quốc gia, lưu không mất thể loại, xoá có hỏi.
- E2E giữ ghế của lượt trước chạy lại: 7/7 PASS.
- Build: dotnet build 0 lỗi; tsc -b sạch; vite build sạch.

CÒN LẠI — CẦN QUYẾT ĐỊNH, CHƯA SỬA
- Chủ đề mặc định: code để nền tối (theme.tsx:13, index.html), README.md:289 ghi nền sáng. Chưa rõ bên nào đúng.
- Quản lý rạp vẫn tạo/sửa được phim — danh mục phim dùng chung toàn hệ thống. Chưa rõ có nên chỉ cho Admin không.
- /mock-gateway/notify ai gọi cũng được (tự ký hộ): đúng thiết kế demo, nhưng không được bật cổng Mock ở môi trường thật.
- Promotion.UsageLimit chỉ kiểm lúc giữ ghế; nhiều đơn Pending cùng lúc có thể vượt giới hạn khi cùng thanh toán.
- Hoàn tiền gọi cổng xong mới commit DB: nếu commit lỗi sau khi cổng đã hoàn thì hai bên lệch (hiếm, cần đối soát).

DEV ĐÃ TỰ VERIFY
[ ] Đã đọc lại toàn bộ nội dung, không copy mù.
[ ] Commit/dòng code đã mở thử và đúng.
[ ] Mục 3 đã grep thật, không đoán.
