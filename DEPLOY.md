# Hướng dẫn Triển khai Miễn phí (Free Deployment Guide) — CineBook MVC

Hướng dẫn này giúp bạn đưa ứng dụng **CineBook ASP.NET Core MVC** lên môi trường Internet thật **hoàn toàn MIỄN PHÍ 100%**.

---

## 🏗️ Kiến trúc Triển khai (Free Stack)

| Thành phần | Dịch vụ | Gói | Ghi chú |
|---|---|---|---|
| **Web App MVC** | [Render.com](https://render.com) | Free Web Service | Chạy Docker container phục vụ cả Giao diện MVC, File tĩnh & APIs |
| **Cơ sở dữ liệu** | [Neon.tech](https://neon.tech) | Free Serverless Postgres | CSDL PostgreSQL trực tuyến tại Singapore (0.5GiB, hỗ trợ SSL) |
| **Cổng thanh toán** | VNPay Sandbox / Mock | Free | Thanh toán giả lập local & sandbox VNPay |

```
Trình duyệt / Khách hàng
           │
           ▼
    Render.com (HTTPS)  https://<ten-app>.onrender.com
    (ASP.NET Core MVC + Static Files + APIs + Razor Views)
           │
           ▼
    Neon PostgreSQL (Singapore - SSL Mode Required)
```

> **Ưu điểm lớn nhất của mô hình MVC**: Toàn bộ dự án được đóng gói thành **duy nhất 1 tệp Docker Image**, không cần tách nhỏ frontend/backend hay cấu hình CORS rắc rối.

---

## Bước 1 — Khởi tạo CSDL PostgreSQL trên Neon.tech

1. Đăng ký tài khoản miễn phí tại <https://neon.tech>.
2. Tạo Project mới đặt tên: `cinebook`, chọn vùng **Singapore (ap-southeast-1)** để có tốc độ nhanh nhất về Việt Nam.
3. Bấm **Connect**, sao chép chuỗi kết nối PostgreSQL (dạng `postgresql://...`).
4. Chạy script chuyển đổi sang định dạng chuỗi ADO.NET Npgsql:

```bash
python3 tools/neon-connstring.py 'postgresql://...dán-chuỗi-neon-vào-đây...'
```

Kết quả thu được dạng:
```
Host=ep-xxx.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=...;SSL Mode=Require;Trust Server Certificate=true
```

> **Lưu ý**: Ứng dụng tích hợp sẵn `Database:AutoMigrate = true` nên khi triển khai lần đầu, hệ thống sẽ **tự động tạo bảng & nạp dữ liệu mẫu (Seed Data)** mà bạn không cần gõ lệnh SQL thủ công.

---

## Bước 2 — Sinh các chuỗi bí mật (Secret Keys)

Tạo các chuỗi ngẫu nhiên để bảo mật môi trường thật:

```bash
openssl rand -base64 32   # Dùng cho Jwt__Key
openssl rand -base64 32   # Dùng cho Qr__Secret
openssl rand -base64 24   # Dùng cho Payment__Mock__Secret
```

---

## Bước 3 — Deploy Web App MVC lên Render.com

1. Truy cập <https://render.com>, chọn **New +** → **Web Service**.
2. Kết nối tới Repository GitHub `CineBook-API` của bạn.
3. Cấu hình thông số như sau:

| Mục | Giá trị |
|---|---|
| **Name** | `cinebook-web` *(hoặc tên tùy chọn)* |
| **Language** | **Docker** |
| **Region** | **Singapore** *(Cùng vùng với Neon CSDL)* |
| **Branch** | `main` |
| **Root Directory** | **Để trống** *(Dockerfile nằm ngay thư mục gốc)* |
| **Instance Type** | **Free** ($0 / tháng) |
| **Dockerfile Path** | `./Dockerfile` *(trong mục Advanced)* |
| **Health Check Path** | `/api/health` *(trong mục Advanced)* |

4. Thêm các **Environment Variables** (Biến môi trường) tại mục Environment:

| Biến môi trường | Giá trị |
|---|---|
| `ConnectionStrings__Default` | Chuỗi kết nối Neon ở **Bước 1** |
| `Jwt__Key` | Chuỗi bí mật sinh ở **Bước 2** |
| `Qr__Secret` | Chuỗi bí mật sinh ở **Bước 2** |
| `Payment__Mock__Secret` | Chuỗi bí mật sinh ở **Bước 2** |
| `Payment__VnPay__TmnCode` | Mã TMN Sandbox VNPay *(Nếu dùng)* |
| `Payment__VnPay__HashSecret` | HashSecret Sandbox VNPay *(Nếu dùng)* |

5. Nhấn **Create Web Service**. Render sẽ tự động kéo code, build Docker container và kích hoạt dịch vụ.
6. Sau khi deploy xong, mở liên kết: `https://<ten-app>.onrender.com/api/health`, nếu trả về `{"status":"ok",...}` là đã thành công!

---

## Bước 4 — Cấu hình Cổng Thanh Toán VNPay Sandbox (Tùy chọn)

Nếu bạn sử dụng cổng thanh toán VNPay thật:

1. Vào Render, bổ sung 2 biến môi trường:
   - `Payment__VnPay__PublicBaseUrl` = `https://<ten-app>.onrender.com`
   - `Payment__VnPay__AppBaseUrl` = `https://<ten-app>.onrender.com`
2. Đăng ký URL trên trang Sandbox VNPay:
   - **Return URL**: `https://<ten-app>.onrender.com/api/payments/vnpay-return`
   - **IPN URL**: `https://<ten-app>.onrender.com/api/payments/vnpay-ipn`

---

## Bước 5 — Kiểm tra Sau Khi Deploy Thành Công

Chạy script kiểm thử trực tiếp trên tên miền thật:

```bash
APP=https://<ten-app>.onrender.com

curl -s $APP/api/health                      # Phải trả về {"status":"ok"}
curl -s $APP/api/payments/providers          # Phải danh sách cổng thanh toán

python3 tests/test_business_flow.py $APP     # Chạy 28 bài kiểm tra nghiệp vụ trên web thật
python3 tests/test_race_condition.py $APP    # Kiểm tra tranh chấp 20 ghế cùng lúc
```

---

## ⚡ Mẹo giữ ứng dụng luôn thức (Keep Alive)

Gói miễn phí của Render sẽ tạm dừng (Sleep) sau 15 phút không có lượt truy cập. Khi có người vào lại sẽ mất khoảng 30-50 giây để khởi động.

**Để giữ web luôn chạy mượt trong ngày bảo vệ môn học:**
1. Thêm biến môi trường `KeepAwake__Enabled` = `true` trên Render.
2. Hoặc đăng ký cron job miễn phí tại <https://cron-job.org> để gọi vào `https://<ten-app>.onrender.com/api/health` mỗi 10 phút/lần.
