# CineBook — Website Đặt Vé Xem Phim (ASP.NET Core 8 MVC)

BTL môn Lập trình Web / Phân tích thiết kế hệ thống · ASP.NET Core 8 MVC + PostgreSQL (Neon / Docker)

Hướng dẫn triển khai miễn phí lên môi trường thật: [`DEPLOY.md`](DEPLOY.md).
File kịch bản khởi tạo CSDL: [`cinebook.sql`](cinebook.sql).

## Yêu cầu máy

| Cần | Ghi chú |
|---|---|
| .NET SDK 8 | Đã cài sẵn hoặc xem lệnh bên dưới |
| **Không** bắt buộc Docker | CSDL có thể chạy online trên Neon (miễn phí) hoặc Docker local |

Cài .NET 8 trên máy mới (nếu chưa có):

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
echo 'export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"' >> ~/.bashrc
dotnet tool install --global dotnet-ef --version 8.0.10
```

## Chuỗi kết nối CSDL

**Không bao giờ commit chuỗi kết nối.** Sử dụng user-secrets ở môi trường local:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=...neon.tech;Database=cinebook;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true" --project src/CineBook.Web
```

Hoặc đặt qua biến môi trường `CINEBOOK_DB`:

```bash
export CINEBOOK_DB="Host=...neon.tech;Database=cinebook;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
```

## Lệnh thường dùng

```bash
dotnet build CineBook.sln                      # Build toàn bộ solution
dotnet run --project src/CineBook.Web          # Chạy ứng dụng MVC (Cổng 5099)
dotnet watch run --project src/CineBook.Web    # Chạy MVC ở chế độ hot-reload
dotnet test                                    # Chạy toàn bộ unit test (24 test cases)
```

Tự động tạo file `.sql` khởi tạo CSDL:

```bash
dotnet ef dbcontext script --project src/CineBook.Infrastructure --startup-project src/CineBook.Web -o cinebook.sql
```

## Cấu trúc Solution

| Project | Chứa gì | Phụ thuộc |
|---|---|---|
| `CineBook.Domain` | Thực thể, enum, quy tắc nghiệp vụ. **Không** phụ thuộc EF Core | — |
| `CineBook.Application` | Service nghiệp vụ: đặt vé, tính giá, voucher, soát vé | Domain |
| `CineBook.Infrastructure` | `DbContext`, migration, cổng thanh toán (Mock + VNPay), gửi mail | Application |
| `CineBook.Web` | **ASP.NET Core MVC**: Controllers, Razor Views, Area Admin, Session/Cookies, RESTful APIs, Swagger | Infrastructure |
| `CineBook.Web.Tests` | 24 Unit tests kiểm thử định dạng, validation, rich text, logic nghệ thuật bìa phim | Web |

## Ghi chú thiết kế & Điểm nổi bật môn học

- **Mô hình MVC chuẩn**: Tổ chức Controllers, Razor Views, Partial Views, Tag Helpers, Area `Admin`.
- **Phân quyền (Roles & Security)**: Tích hợp Cookie Authentication cho Web MVC + JWT Bearer cho REST API. Gồm 4 vai trò: `Customer`, `Staff`, `CinemaManager`, `Admin`.
- **Session & Cookies**: Dùng `HttpContext.Session` để quản lý trạng thái giữ ghế/đơn đặt vé đang chọn (`BookingId`). Dùng Cookies lưu thông tin đăng nhập và chủ đề giao diện.
- **Paging, Filtering & AJAX**: Danh sách phim có lọc thể loại/trạng thái/tìm kiếm AJAX, sơ đồ chọn ghế cập nhật tự động 5s/lần qua AJAX.
- **Validation & Annotations**: Kiểm tra dữ liệu 2 lớp với DataAnnotations (`[Required]`, `[Range]`, `[StringLength]`, `[EmailAddress]`) kết hợp client-side jQuery Unobtrusive validation.
- **Rich Text Editor**: Tích hợp CKEditor 5 cho textarea mô tả phim trong giao diện quản trị.
- **Upload tệp tin**: Upload poster phim lưu vào `wwwroot/images/posters/`.
- **Cổng thanh toán**: Hỗ trợ 2 cổng thanh toán chạy song song:
  1. `MockPaymentGateway` (Mặc định bật, hỗ trợ demo/bảo vệ offline không cần mạng, thử nghiệm webhook trùng).
  2. `VnPayGateway` (Cổng VNPay Sandbox qua thẻ ATM, QR Code, thẻ quốc tế).

## Tài khoản mẫu

Dữ liệu mẫu khởi tạo sẵn 4 vai trò, mật khẩu mặc định đều là `123456`:

| Email | Vai trò | Chức năng thử nghiệm |
|---|---|---|
| `admin@cinebook.vn` | Admin | Toàn quyền trang `/Admin`: quản lý phim, rạp, suất chiếu, voucher, báo cáo |
| `manager@cinebook.vn` | Quản lý rạp | Chỉ thấy dữ liệu rạp Cầu Giấy |
| `staff@cinebook.vn` | Nhân viên rạp | Soát vé QR tại `/CheckIn` |
| `khach@gmail.com` | Khách hàng | Chọn ghế, mua vé, lưu voucher, tải ảnh vé |

Mã giảm giá có sẵn: `CHAOBAN` (giảm 20%, tối đa 50.000đ) và `THU3VUI` (giảm 30.000đ).

## Kiểm thử

```bash
# Chạy ứng dụng trước tại http://localhost:5099
python3 tests/test_business_flow.py      # 28 kiểm tra nghiệp vụ đầu-cuối
python3 tests/test_race_condition.py     # 20 người giành cùng một ghế cùng lúc
```

## Chạy offline (với Docker Postgres)

```bash
docker run -d --name cinebook-db -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=cinebook -p 5432:5432 postgres:16-alpine
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=cinebook;Username=postgres;Password=postgres" --project src/CineBook.Web
dotnet run --project src/CineBook.Web
```

---

## Cổng thanh toán VNPay (Sandbox)

### 1. Đặt khoá sandbox bằng user-secrets

```bash
dotnet user-secrets set "Payment:VnPay:TmnCode"    "MÃ_TMN_CỦA_BẠN"  --project src/CineBook.Web
dotnet user-secrets set "Payment:VnPay:HashSecret" "CHUỖI_BÍ_MẬT"    --project src/CineBook.Web
```

### 2. Thẻ thử nghiệm Sandbox VNPay

| Trường | Giá trị |
|---|---|
| Ngân hàng | NCB |
| Số thẻ | `9704198526191432198` |
| Chủ thẻ | `NGUYEN VAN A` |
| Ngày phát hành | `07/15` |
| Mật khẩu OTP | `123456` |
