[BUG] Đơn chờ thanh toán chặn ghế: người đặt không thanh toán tiếp được, người khác không chọn được ghế

Ticket: [DEV ĐIỀN]
Màn hình: Đặt vé (chọn ghế → bắp nước & ưu đãi → thanh toán), Kết quả thanh toán, Vé của tôi, Trang chủ
Branch / commit: main / chưa commit — base BE 58fcdf3 (2026-09-27, "Tách thư mục web ra repo CineBook riêng"), base FE (repo web/) 5200ea7 (2026-09-27, "Form tạo suất chiếu: đặt riêng giá ghế thường, VIP, ghế đôi")
Người fix / ngày: [DEV ĐIỀN] / 2026-09-30

0. ĐỐI CHIẾU TÀI LIỆU KHÁCH
- Không tìm thấy tài liệu nghiệp vụ gốc trong workspace: README.md:5 trỏ tới ../danh-gia-nghiep-vu-btl/ nhưng thư mục này không tồn tại.
- Luật chỉ còn được trích lại trong code: PaymentService.cs:132 "BR-10: không nhả ghế ở đây — khách vẫn được thử lại trong thời gian giữ ghế còn lại."; BookingOptions.cs:11 "BR-02: thời gian giữ ghế" (8 phút).
- Hành vi khi hết giờ: README.md:314 (tài liệu nội bộ) ghi "hết giờ tự đưa khách về bước chọn ghế". Yêu cầu mới ngày 2026-09-30: hết 8 phút thì đưa về trang chủ → đã sửa README.md:314 theo yêu cầu mới.
Kết luận: (B) Có yêu cầu (BR-10: được thử lại trong thời gian giữ ghế), mình làm thiếu ở luồng FE và ở lần giữ ghế lại. Riêng "hết giờ về trang chủ" là yêu cầu mới (A). Cần đối chiếu BR-10 với bản gốc khi tìm được tài liệu.

1. NGUYÊN NHÂN TECH
Tái hiện bằng API trên DB thử (khach@gmail.com giữ A1, A2; cổng giả lập báo thất bại; khách F5 rồi chọn lại đúng A1, A2):
- Khách A nhận 409 SEATS_TAKEN "Một số ghế vừa có người khác chọn" với chính ghế của mình.
- Khách B cũng nhận 409 trong 8 phút.
- Sau khi hết hạn, đơn vẫn ở trạng thái Pending, và "Vé của tôi" vẫn hiện "Chờ thanh toán".
1) Chọn lại ghế của chính mình bị từ chối — BookingService.cs trước fix, dòng 129-130: câu UPDATE giành ghế chỉ nhận ghế "Status = Available OR (Held AND HoldExpiresAtUtc < now)". Ghế đang Held bởi đơn Pending còn hạn của chính khách đó không khớp, nên affected != seatIds.Length và trả SEATS_TAKEN. HoldSeatsAsync không đóng đơn Pending cũ của cùng khách và cùng suất trước khi giữ ghế lại.
2) Khách không có đường quay lại đơn đang giữ:
   - Booking.tsx trước fix, dòng 38: đơn chỉ nằm trong useState, bấm F5 hay back là mất. Trang luôn mở ở bước 'seats'.
   - BookingResult.tsx dòng 27 và 88: thanh toán thất bại chỉ có nút "Tiếp tục đặt vé" (sang /phim), không có nút thanh toán lại.
   - MyTickets.tsx dòng 50: chỉ đơn Paid mới có thao tác.
   - PaymentsController.cs dòng 176: trang cổng giả lập chỉ redirect khi thành công, bấm "Thanh toán thất bại" thì khách kẹt ở trang cổng.
   → Ghế bị khoá đủ 8 phút cho cả khách A lẫn khách B.
3) Đơn quá hạn vẫn được coi là còn sống, vì job dọn đơn 30 phút mới chạy một lần (appsettings.json, Booking:ExpireJobIntervalMinutes):
   - BookingService.cs dòng 392: ToDto trả nguyên b.Status (Pending).
   - BookingService.cs dòng 370: "Vé của tôi" chỉ lọc Status != Expired.
   - PromotionService.cs dòng 45: đếm cả đơn Pending đã hết hạn vào lượt dùng voucher của khách.
   - Hệ quả: trang kết quả poll 1,5 giây một lần cho tới khi job chạy, và khách dùng lại voucher thì nhận "Bạn đã dùng mã này rồi".
4) Hết giờ — Booking.tsx trước fix, dòng 364: onExpire chỉ đưa về bước chọn ghế, chưa đúng yêu cầu mới là về trang chủ.

2. CÁCH SỬA
1) BookingService.cs:111, 184: thêm CloseOwnPendingAsync, chạy trong transaction của HoldSeatsAsync, trước câu UPDATE giành ghế. Hàm này đóng các đơn Pending của cùng khách và cùng suất: đơn còn hạn thì ghi Cancelled ("Khách chọn lại ghế"), đơn đã quá hạn thì ghi Expired, rồi nhả ghế. Nếu giữ ghế thất bại, transaction rollback và đơn cũ được giữ nguyên.
2) BookingService.cs:430: đơn Pending có secondsLeft = 0 thì trả về status Expired ngay, không chờ job. BookingService.cs:406: "Vé của tôi" bỏ đơn Pending đã quá hạn.
3) PromotionService.cs:47: chỉ đếm đơn Pending còn hạn (HoldExpiresAtUtc > now) vào lượt dùng voucher.
4) PaymentsController.cs:180: cổng giả lập redirect cả khi thất bại, kèm tham số payment=failed.
5) FE Booking.tsx:
   - Dòng 50-67: khi mở trang, hỏi /api/bookings/mine (không cache). Nếu còn đơn Pending chưa hết hạn cho suất này thì nhảy thẳng tới bước thanh toán, kèm đồng hồ đếm ngược và dòng "Bạn đang có đơn giữ ghế cho suất này".
   - Dòng 70 và 404: hết giờ thì navigate('/') kèm thông báo.
6) FE Home.tsx:27, 63: hiện banner "Đã hết thời gian giữ ghế…", có nút đóng.
7) FE BookingResult.tsx:
   - Dòng 15: nhận thêm payment=failed là dấu hiệu thất bại.
   - Dòng 31 và 91: nếu cổng báo thất bại mà đơn còn Pending và còn giờ thì hiện nút "Thanh toán lại" (sang /dat-ve/<suất>, trang này tự nối lại đơn).
8) FE MyTickets.tsx:119: đơn Pending có link "Tiếp tục thanh toán". SeatMap.tsx:147: ghế heldByMe hiển thị như ghế chọn được.
9) README.md:314: cập nhật hành vi khi hết giờ và khi nối lại đơn.

3. CHECK LỖI TƯƠNG TỰ
- Đã grep "BookingStatus.Pending|SeatStatus.Held" trong src:
  - PaymentService.cs:58: CreateAsync có kiểm HoldExpiresAtUtc ở dòng 60 → không bị.
  - ShowtimeService.cs:172: đếm ghế Held quá hạn là ghế trống → không bị.
  - BookingService.cs:68: sơ đồ ghế cũng coi Held quá hạn là trống → không bị.
  - ReportService chỉ lọc Paid/Refunded → không bị.
- BookingService.cs:292, ConfirmPaidAsync: KHÔNG kiểm tra đơn còn Pending mới được xác nhận. Nếu một đơn Pending đã bị CloseOwnPendingAsync (hoặc nút "Hủy và chọn lại ghế", tức CancelPendingAsync) đóng trong khi giao dịch VNPay của đơn đó vẫn đang xử lý, webhook thành công về sau sẽ trả INVALID_STATE: tiền đã trừ mà không ra vé. Lỗi này có từ trước fix, fix làm tăng tần suất gặp. Chưa sửa ở lượt này.
- BookingService.cs:303: VNPay ExpireMinutes = 15 (VnPayGateway.cs:53) lớn hơn thời gian giữ ghế 8 phút, nên khách trả tiền sau phút thứ 8 có thể rơi vào SEATS_LOST, và payment Succeeded cũng không được hoàn. Có từ trước fix, chưa sửa.
- FE: đã grep "onExpire|<Countdown": chỉ có Booking.tsx:404. Grep "'Pending'": BookingResult.tsx:21 (poll khi Pending) — BE nay trả Expired nên poll dừng đúng lúc.

4. PHẠM VI ẢNH HƯỞNG
- HoldSeatsAsync: chỉ được gọi từ BookingsController.cs:23 (POST /api/bookings/hold). Cần regression luồng đặt vé và chống giành ghế.
- ToDto/MapAsync: được gọi từ BookingsController.cs:28 (GET /api/bookings/{id}) và BookingsController.cs:34 (/mine). Trường status có thể trả Expired sớm hơn trước. Màn hình bị ảnh hưởng: Kết quả thanh toán, Vé của tôi.
- EvaluateAsync (shared): được gọi từ BookingService.cs:253 (áp mã lúc giữ ghế) và PromotionService.cs:149 (danh sách voucher ở CatalogController.cs:95 và 101). Màn hình bị ảnh hưởng: bước Bắp nước & ưu đãi, trang Ưu đãi.
- Cổng giả lập /mock-gateway: nút "Thanh toán thất bại" nay redirect về trang kết quả.
- Không đổi schema, không cần migration.

5. LÝ DO LỌT BUG — 5WHY (Dev)
Why1: Vì sao khách A không thanh toán tiếp được? → Vào lại trang đặt vé thì trang mở ở bước chọn ghế (Booking.tsx:38, state cũ đã mất). Chọn lại đúng ghế của mình thì API trả 409 SEATS_TAKEN (đã tái hiện).
Why2: Vì sao API từ chối ghế của chính khách đó? → Câu UPDATE ở BookingService.cs:129-130 không phân biệt ghế Held bởi chính khách hay bởi người khác, và đơn Pending cũ không được đóng trước khi giữ ghế lại.
Why3: Vì sao đơn Pending cũ vẫn còn đó mà khách không làm gì tiếp được? → FE không có đường nào quay về một đơn Pending: không nối lại khi F5, không có nút thanh toán lại ở trang kết quả, "Vé của tôi" không có thao tác cho đơn Pending, cổng giả lập không redirect khi thất bại. BR-10 ("được thử lại trong thời gian giữ ghế") mới chỉ làm ở BE (PaymentService.cs:132 không nhả ghế; CreateAsync cho tạo thanh toán lại).
Why4: Vì sao không test nào bắt được? → tests/test_business_flow.py (28 kiểm tra) chỉ có kịch bản người KHÁC giành ghế (mục 6). Không có kịch bản cùng một khách giữ ghế lần hai, thanh toán thất bại rồi thử lại, hay hết hạn giữ ghế.
Why5: Vì sao bộ test không có các kịch bản đó? → Các luật về thời gian giữ ghế (BR-02, BR-10) chỉ được kiểm ở phía "chống người khác giành ghế". Luồng khách rời trang giữa chừng (F5, back, thất bại ở cổng) không có trong danh sách kịch bản kiểm thử của luồng đặt vé.
Root cause chính: Thiếu test case (luồng khách rời trang giữa chừng và quay lại trong thời gian giữ ghế) → cần thêm vào tests/test_business_flow.py 3 kịch bản: cùng khách giữ ghế lần hai, thất bại rồi thanh toán lại, và hết hạn giữ ghế.
Jira > Cause Category: IMPLEMENT_Insufficient analysis before implementation
Jira > Defect Type: Fun_Incomplete Function

KIỂM CHỨNG ĐÃ CHẠY (Postgres 16 local trong docker, không dùng Neon)
- Script API sau fix: 15/15 PASS. Gồm:
  - khách giữ lại được đúng ghế cũ kèm cùng voucher, đơn cũ chuyển Cancelled, chỉ còn 1 đơn Pending;
  - B vẫn bị chặn trong 8 phút;
  - hết hạn thì API trả Expired, "Vé của tôi" ẩn đơn, voucher dùng lại được, B giữ ghế và thanh toán được;
  - giữ ghế thất bại thì rollback, đơn cũ không bị đổi.
- Test có sẵn: tests/test_business_flow.py 28/28 đạt. tests/test_race_condition.py: 20 luồng giành 1 ghế, đúng 1 người giữ được.
- E2E trình duyệt (headless Chromium, HoldMinutes = 1): 7/7 PASS. Gồm:
  - thất bại ở cổng giả lập → có nút "Thanh toán lại" → nối lại đúng đơn;
  - F5 vẫn nối lại được;
  - hết giờ → về trang chủ kèm banner, đóng được banner;
  - vào lại suất đó thì ghế đã trống.
- FE: tsc -b sạch, vite build sạch. oxlint còn 1 cảnh báo set-state-in-effect ở Booking.tsx:63 (effect nối lại đơn, chạy đúng 1 lần nhờ ref).

DEV ĐÃ TỰ VERIFY
[ ] Đã đọc lại toàn bộ nội dung, không copy mù.
[ ] Commit/dòng code đã mở thử và đúng.
[ ] Mục 3 đã grep thật, không đoán.
