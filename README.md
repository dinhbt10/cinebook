# CineBook — Website đặt vé xem phim

BTL môn Phân tích thiết kế hướng đối tượng · ASP.NET Core 8 Web API + React SPA + PostgreSQL (Neon)

Tài liệu phân tích nằm ở [`../danh-gia-nghiep-vu-btl/`](../danh-gia-nghiep-vu-btl/).
Hướng dẫn đưa lên môi trường thật: [`DEPLOY.md`](DEPLOY.md).

## Yêu cầu máy

| Cần | Ghi chú |
|---|---|
| .NET SDK 8 | `~/.dotnet` đã cài sẵn trên máy này; máy khác xem mục dưới |
| Node.js 20+ | cho phần React |
| **Không** cần Docker, Postgres hay Redis | CSDL chạy online trên Neon |

Cài .NET 8 trên máy mới (không cần quyền root):

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
echo 'export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"' >> ~/.bashrc
dotnet tool install --global dotnet-ef --version 8.0.10
```

## Chuỗi kết nối

**Không bao giờ commit chuỗi kết nối.** Dùng user-secrets ở local:

```bash
dotnet user-secrets init  --project src/CineBook.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=...neon.tech;Database=cinebook;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true" --project src/CineBook.Api
```

Lệnh `dotnet ef` đọc biến môi trường `CINEBOOK_DB`:

```bash
export CINEBOOK_DB="Host=...neon.tech;Database=cinebook;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
```

## Lệnh thường dùng

```bash
dotnet build                                   # build toàn bộ solution
dotnet watch run --project src/CineBook.Api    # chạy API (kèm giao diện đã build)
cd web && npm run dev                          # chạy React ở chế độ phát triển (cổng 5111 cố định)
cd web && npm run build                        # build React vào wwwroot của API
dotnet ef database update --project src/CineBook.Infrastructure \
        --startup-project src/CineBook.Infrastructure   # áp migration lên CSDL
dotnet ef migrations add <Tên> --project src/CineBook.Infrastructure \
        --startup-project src/CineBook.Infrastructure \
        --output-dir Persistence/Migrations             # tạo migration mới
```

> ⚠️ **Chỉ một người trong nhóm tạo migration.** Hai người còn lại `git pull` rồi chạy `database update`. Đây là rủi ro số 1 trong tài liệu đánh giá công nghệ.

## Cấu trúc

| Project | Chứa gì | Phụ thuộc |
|---|---|---|
| `CineBook.Domain` | Thực thể, enum, quy tắc nghiệp vụ. **Không** phụ thuộc EF Core | — |
| `CineBook.Application` | Service nghiệp vụ: đặt vé, tính giá, sinh vé | Domain |
| `CineBook.Infrastructure` | `DbContext`, migration, cổng thanh toán (Mock + VNPay), gửi mail | Application |
| `CineBook.Api` | Controller, xác thực JWT, Swagger, phục vụ bản build React | Infrastructure |
| `web/` | React 19 + Vite + TypeScript + Tailwind | — |

## Ghi chú thiết kế

- **Không dùng Redis.** Việc giữ ghế 8 phút và chống đặt trùng làm bằng một câu `UPDATE` có điều kiện trên bảng `ShowtimeSeats` — xem §6.1 của `03-danh-gia-cong-nghe.md`.
- **Toàn bộ thời gian lưu ở UTC**, chỉ đổi sang giờ Việt Nam khi hiển thị.
- **Cổng thanh toán** dùng interface `IPaymentGateway`, có hai cài đặt chạy song song:
  `MockPaymentGateway` (mặc định, không cần mạng) và `VnPayGateway` (sandbox VNPay).
  Khách chọn cổng ngay ở bước thanh toán; cổng nào thiếu cấu hình thì không hiện ra.

## Tài khoản mẫu

Dữ liệu mẫu tạo sẵn bốn vai trò, mật khẩu đều là `123456`:

| Email | Vai trò | Dùng để |
|---|---|---|
| `admin@cinebook.vn` | Admin | Toàn quyền, xem báo cáo toàn hệ thống |
| `manager@cinebook.vn` | Quản lý rạp | Chỉ thấy dữ liệu rạp Cầu Giấy (BR-19) |
| `staff@cinebook.vn` | Nhân viên rạp | Soát vé QR |
| `khach@gmail.com` | Khách hàng | Đặt vé |

Mã giảm giá có sẵn: `CHAOBAN` (giảm 20%, tối đa 50.000đ) và `THU3VUI` (giảm 30.000đ).

## Kiểm thử

```bash
# API phải đang chạy
python3 tests/test_business_flow.py      # 28 kiểm tra nghiệp vụ đầu-cuối
python3 tests/test_race_condition.py     # 20 người giành cùng một ghế cùng lúc
```

`test_race_condition.py` là bằng chứng cho BR-01 khi bảo vệ: 20 luồng cùng gửi yêu cầu
giữ đúng một ghế, kết quả phải luôn là **1 người giữ được, 19 người nhận `SEATS_TAKEN`**.

## Chạy offline (khi mất mạng, không dùng được Neon)

```bash
docker run -d --name cinebook-db -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=cinebook \
  -p 55432:5432 postgres:16-alpine
export ConnectionStrings__Default="Host=localhost;Port=55432;Database=cinebook;Username=postgres;Password=postgres"
```

## Cổng thanh toán VNPay (sandbox)

### 1. Lấy khoá sandbox

Đăng ký tại <https://sandbox.vnpayment.vn/devreg/>, VNPay gửi về email hai giá trị:
`vnp_TmnCode` (mã website) và `vnp_HashSecret` (chuỗi bí mật để ký).

### 2. Đặt khoá bằng user-secrets (KHÔNG commit)

```bash
dotnet user-secrets set "Payment:VnPay:TmnCode"    "MÃ_TMN_CỦA_BẠN"  --project src/CineBook.Api
dotnet user-secrets set "Payment:VnPay:HashSecret" "CHUỖI_BÍ_MẬT"    --project src/CineBook.Api
```

Chưa đặt hai khoá này thì cổng VNPay tự ẩn khỏi màn hình thanh toán, chỉ còn cổng giả lập —
nên cả nhóm vẫn chạy được dự án mà không cần ai cũng có tài khoản sandbox.

### 3. Địa chỉ quay lại

`appsettings.Development.json` đã đặt sẵn cho môi trường dev:

| Khoá | Giá trị dev | Ý nghĩa |
|---|---|---|
| `Payment:VnPay:PublicBaseUrl` | `http://localhost:5111` | gốc dùng để dựng `vnp_ReturnUrl` |
| `Payment:VnPay:AppBaseUrl` | `http://localhost:5111` | nơi đưa khách về sau khi thanh toán |

Khi deploy chung một artifact thì để trống cả hai, hệ thống tự lấy theo địa chỉ request.

> ⚠️ **Cả hai đều trỏ cổng 5111 của vite, KHÔNG phải cổng 5099 của API.**
>
> `vnp_ReturnUrl` là địa chỉ mà **trình duyệt của khách** phải mở được, chứ không phải
> địa chỉ nội bộ giữa các tiến trình. Khi lập trình qua SSH hoặc VS Code Remote,
> thường chỉ cổng 5111 được chuyển tiếp ra máy cá nhân còn 5099 thì không — để
> `vnp_ReturnUrl` trỏ 5099 sẽ khiến thanh toán xong quay về là trình duyệt báo lỗi
> không mở được trang, dù giao dịch đã ghi nhận thành công trong CSDL.
>
> Vite đã proxy sẵn `/api` sang 5099 nên đường quay lại đi qua 5111 vẫn tới đúng
> controller. Kiểm tra nhanh xem cổng nào đang được chuyển tiếp: `ss -lntp | grep node`.

Khai báo trên cổng VNPay:

- **Return URL** — `http://localhost:5111/api/payments/vnpay-return`
- **IPN URL** — `https://<địa-chỉ-công-khai>/api/payments/vnpay-ipn`

### 4. Chọn cách thanh toán (quét QR / thẻ ATM / thẻ quốc tế)

Khách chọn VNPay xong chọn tiếp cách trả tiền; hệ thống gửi kèm `vnp_BankCode` nên
sang tới VNPay là vào thẳng màn hình tương ứng, không phải bấm thêm một bước.

| Lựa chọn trên giao diện | `vnp_BankCode` | Màn hình VNPay hiện ra |
|---|---|---|
| **Quét mã VNPAY-QR** (mặc định) | `VNPAYQR` | **Mã QR để quét bằng app ngân hàng / ví** |
| Thẻ ATM / Internet Banking | `VNBANK` | Danh sách ngân hàng nội địa |
| Thẻ quốc tế Visa, Master | `INTCARD` | Form nhập thẻ quốc tế |
| Để VNPay tự hỏi | *(không gửi)* | Trang chọn phương thức của VNPay |

Đổi mặc định bằng `Payment:VnPay:DefaultMethod` trong `appsettings.json`.

> **Mã QR do máy chủ VNPay sinh và ký, không phải do CineBook vẽ ra.** Vì vậy bắt buộc
> phải có `TmnCode` + `HashSecret` ở bước 2 thì mới có QR quét được. Chưa có khoá thì
> cổng VNPay không hiện trên giao diện.

#### Vì sao mặc định là "Trang VNPay" chứ không phải đi thẳng vào QR

Tài khoản sandbox mới cấp **chưa được bật kênh VNPAYQR**. Gửi thẳng
`vnp_BankCode=VNPAYQR` sẽ bị VNPay trả về `Payment/Error.html?code=76` —
*"Ngân hàng thanh toán không được hỗ trợ"*. Vì vậy mặc định để trống `vnp_BankCode`,
VNPay mở trang chọn phương thức của họ, trong đó đã có sẵn mục quét mã QR.

Muốn đi thẳng vào màn hình QR thì liên hệ VNPay xin bật kênh VNPAYQR cho mã website
(hotrovnpay@vnpay.vn), sau đó đặt `Payment:VnPay:DefaultMethod` = `VNPAYQR`.

#### Bảng mã lỗi lúc khởi tạo giao dịch

VNPay không công bố các mã này trong tài liệu; đây là kết quả thử nghiệm trực tiếp
với sandbox, dùng để chẩn đoán nhanh khi link thanh toán bị từ chối:

| Mã trên `Payment/Error.html?code=` | Nguyên nhân |
|---|---|
| `70` | Sai chữ ký `vnp_SecureHash` |
| `72` | `vnp_TmnCode` không tồn tại |
| `76` | Kênh thanh toán (`vnp_BankCode`) chưa bật cho website này |

Ngoài ra máy chủ VNPay có tường lửa chống gọi tự động: gửi liên tiếp nhiều yêu cầu
bằng script sẽ nhận `403 Forbidden` từ openresty thay vì trang thanh toán. Thử bằng
trình duyệt thật hoặc giãn nhịp gọi.

### 5. Thẻ thử của sandbox

| Trường | Giá trị |
|---|---|
| Ngân hàng | NCB |
| Số thẻ | `9704198526191432198` |
| Chủ thẻ | `NGUYEN VAN A` |
| Ngày phát hành | `07/15` |
| Mật khẩu OTP | `123456` |

### 6. Vì sao đường quay lại cũng ghi nhận thanh toán

Đúng lý thuyết thì **IPN** mới là nguồn sự thật (BR-07), còn đường khách quay lại chỉ để
hiển thị. Nhưng máy chủ VNPay không gọi được vào `localhost`, nên ở môi trường phát triển
`/api/payments/vnpay-return` cũng ghi nhận kết quả. Việc này an toàn vì:

1. Chữ ký HMAC-SHA512 vẫn được kiểm tra trước khi đụng tới đơn hàng.
2. Ghi nhận thanh toán là **idempotent** (BR-08) — nếu sau đó IPN vẫn về, hệ thống chỉ trả
   "Đã xử lý trước đó, bỏ qua".

Muốn thử IPN thật thì mở một đường hầm ra ngoài, ví dụ `ngrok http 5099`, rồi khai báo
địa chỉ ngrok làm IPN URL trên cổng VNPay.

### 7. Hoàn tiền

`Payment:VnPay:SimulateRefund` mặc định `true`: tài khoản sandbox không có số dư thật nên
lệnh hoàn tiền chỉ được ghi log, luồng hủy vé vẫn chạy trọn vẹn khi demo. Đặt `false` để
gọi API hoàn tiền thật của VNPay — code gọi API đã viết đầy đủ trong `VnPayGateway.RefundAsync`.

## Thử luồng thanh toán

Cổng thanh toán giả lập nằm ở `/mock-gateway`. Trang này có ba nút, trong đó nút
**"Gửi webhook 2 lần"** dùng để chứng minh BR-08 khi demo: hệ thống chỉ ghi nhận
lần đầu, lần thứ hai trả về "Đã xử lý trước đó, bỏ qua".


## Giao diện

Chạy `npm run dev` trong `web/` để phát triển — giao diện luôn ở **cổng 5111**
(`strictPort` bật nên cổng bận là báo lỗi chứ không tự nhảy cổng), proxy sẵn `/api` sang 5099.
hoặc `npm run build` để đóng gói vào `wwwroot` và chỉ cần chạy API là có cả web.

**Trang khách hàng** — `/`

| Đường dẫn | Nội dung |
|---|---|
| `/` | Trang chủ: hero xoay vòng ba phim nổi bật, dải số liệu, lưới phim, suất chiếu hôm nay, ưu đãi, ba bước đặt vé |
| `/phim` | Danh sách phim, tìm kiếm, lọc theo thể loại và trạng thái |
| `/phim/:id` | Chi tiết phim và lịch chiếu 7 ngày, nhóm theo rạp |
| `/dat-ve/:showtimeId` | **Luồng đặt vé ba bước**: chọn ghế → bắp nước & mã giảm giá → thanh toán |
| `/dat-ve/ket-qua/:id` | Kết quả thanh toán, tự cập nhật khi webhook về |
| `/ve-cua-toi` | Vé đã mua, mã QR, **tải vé về máy dạng ảnh**, hủy vé |
| `/rap` | Hệ thống rạp |
| `/uu-dai` | **Kho voucher**: lưu voucher vào kho, thêm bằng mã, chép mã |

**Trang nhân viên** — `/soat-ve`: quét mã QR bằng camera hoặc nhập mã vé, sáu kết quả
soát vé hiện bằng màu và biểu tượng riêng.

**Trang quản trị** — `/admin`: tổng quan có biểu đồ doanh thu và tỷ lệ lấp đầy,
quản lý phim, tạo suất chiếu (tự chặn chồng giờ), tạo phòng chiếu sinh ghế tự động, báo cáo.

### Tải vé về máy

Mỗi vé có nút **Tải vé**, đơn nhiều ghế có thêm **Tải tất cả**. File tải về là ảnh PNG
1260×1860 gồm mã QR cỡ lớn, số ghế, mã vé, tên phim, rạp, suất chiếu và tổng tiền —
khách đưa nhân viên soát mà không cần mở lại website hay còn mạng.

Không thêm thư viện nào: mã QR vốn đã là thẻ `<svg>` trong DOM, `web/src/lib/ticketImage.ts`
tuần tự hoá nó rồi vẽ lên `canvas` cùng phần chữ, xuất ở tỷ lệ 3× cho nét khi phóng to.

Đã kiểm chứng bằng cách giải mã ngược mã QR trong chính file tải về: chuỗi đọc được
trùng khớp payload của API, và đưa chuỗi đó cho `/api/checkin/scan` thì hệ thống nhận
ra vé thật (trả về trạng thái theo khung giờ, không phải `VE_GIA`).

### Kho voucher (BR-05)

Trước đây khách phải nhớ và gõ đúng mã giảm giá. Nay có bảng `SavedVouchers`
(khoá chính ghép `UserId` + `PromotionId`) làm kho riêng của từng người:

- Trang **Ưu đãi** có nút lưu / bỏ lưu từng voucher, và ô thêm bằng mã.
- Bước **Bắp nước & ưu đãi** hiện danh sách bấm chọn, voucher trong kho xếp trước.
- Mỗi voucher hiện **số tiền giảm thật cho đúng đơn đó**, hoặc lý do chưa dùng được
  (`Cần đơn từ 150.000đ (còn thiếu 100.000đ)`, `Bạn đã dùng mã này rồi`).

Điểm đáng nói khi bảo vệ: luật xét voucher chỉ viết **một lần** ở
`PromotionService.EvaluateAsync`, dùng chung cho cả danh sách hiển thị lẫn
`BookingService` lúc chốt đơn. Nhờ vậy con số khách nhìn thấy trước khi bấm luôn
đúng bằng con số hệ thống trừ — không thể lệch vì hai nơi viết luật riêng.

> ⚠️ Tính năng này kèm migration `AddSavedVouchers`. Người khác trong nhóm chỉ cần
> `git pull` rồi chạy `dotnet ef database update`, **đừng tạo thêm migration**.

### Chủ đề sáng / tối

Bảng màu lấy theo Netflix: đỏ `#E50914` làm màu thương hiệu, thang xám trung tính
(`#141414` nền, `#1F1F1F` thẻ, `#333` viền ở nền tối). Mặc định là **nền sáng**
(xám rất nhạt, không dùng trắng tinh cho đỡ chói), có nút đổi sang nền tối ở góc
phải thanh điều hướng, lựa chọn lưu trong `localStorage`.

Token màu tên là `brand-*` chứ không phải `gold-*` nữa. Chữ đặt trên nền thương hiệu
dùng token riêng `on-brand` (luôn trắng) — không dùng màu nền trang, vì ở nền tối
màu đó là gần đen, đặt lên nền đỏ sẽ không đọc được.

Cách làm: thang màu `ink-950 … ink-500` được định nghĩa lại theo *vai trò* chứ không
theo độ tối — `ink-950` luôn là nền trang, số càng nhỏ càng nổi lên trên. Đổi chủ đề
chỉ là đổi giá trị biến CSS, không phải sửa lại lớp tiện ích ở từng thành phần.

Riêng vài vùng **luôn tối bất kể chủ đề** vì đặt chữ đè lên ảnh sẫm màu: hero trang
chủ, đầu trang chi tiết phim, bìa phim sinh tự động và khoang ghế. Các vùng này gắn
lớp `.on-dark`, bên trong đặt lại đúng bộ biến màu tối.

### Vài điểm trong giao diện đáng chú ý khi bảo vệ

- **Sơ đồ ghế** dựng như một khoang rạp tối đặt trên nền trang sáng: màn hình chiếu là
  cung cong có chùm sáng loe xuống, hàng từ 8 ghế trở lên tự chừa lối đi ở giữa,
  ghế VIP có gạch nhỏ dưới chân. Tự gọi lại mỗi 5 giây nên thấy ngay ghế người khác vừa giữ.
- **Ghế người khác đang giữ tô hổ phách, không tô đỏ.** Đỏ đã là màu ghế mình đang chọn;
  dùng chung một màu cho hai trạng thái thì mất đúng thứ khách cần phân biệt nhanh nhất.
- **Mọi phần tử bấm được đều có con trỏ bàn tay.** Tailwind 4 bỏ `cursor: pointer` mặc
  định của `<button>`, nên `index.css` khai báo lại cho `button`, `[role=button]`,
  `select`, `summary`, `label[for]`, và đổi thành dấu cấm khi bị vô hiệu hoá.
- **Đồng hồ đếm ngược** 8 phút chuyển màu đỏ khi còn dưới một phút, hết giờ tự đưa khách về trang chủ kèm thông báo.
  Còn trong 8 phút mà khách F5, bấm back hay thanh toán thất bại thì vào lại trang đặt vé sẽ nối tiếp đúng đơn đang giữ.
- Khi hai người giành cùng một ghế, người bấm sau nhận thông báo rõ ràng và sơ đồ tự làm mới.
- **Cổng thanh toán giả lập** có nút "Gửi webhook 2 lần" để chứng minh chống xử lý trùng ngay trên màn hình.
- **Bìa phim sinh tự động**: phim chưa có ảnh thì giao diện tự dựng bìa bằng gradient và
  hoạ tiết tia chiếu, suy ra từ chính tên phim (`web/src/lib/art.ts`) — mỗi phim một sắc
  riêng, ổn định giữa các lần tải, không cần mạng và không phải lưu thêm gì vào CSDL.
- **Trailer**: trường `TrailerUrl` dán ở trang quản trị sẽ hiện nút phát trên poster và
  mở cửa sổ xem trailer nhúng từ YouTube.
