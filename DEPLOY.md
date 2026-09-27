# Triển khai CineBook

Giao diện chạy trên **Vercel**, API chạy trên **Render**, cơ sở dữ liệu là **Neon**.
Cả ba đều dùng gói miễn phí.

Dự án tách làm **hai repo**, mỗi cái deploy một nơi:

| Repo | Nội dung | Deploy |
|---|---|---|
| `CineBook-API` | Backend: `Dockerfile`, `src/`, `tests/` | Render |
| `CineBook` | Frontend: chính là nội dung thư mục `web/` | Vercel |

Ở máy, thư mục `web/` nằm lồng bên trong nhưng có `.git` riêng, nên repo backend ghi
nhận nó thành một gitlink rỗng. Vô hại với Docker (`.dockerignore` đã loại `web`),
nhưng trên GitHub nó hiện thành thư mục xám rỗng. Muốn gọn thì trong repo backend chạy
`git rm --cached web` rồi thêm `web/` vào `.gitignore`.

```
Trình duyệt
    │
    ▼
Vercel  https://cine-book-mocha.vercel.app          ← một tên miền duy nhất
    ├── /                → SPA tĩnh
    ├── /api/*           → rewrite ⟶ Render
    └── /mock-gateway/*  → rewrite ⟶ Render
                              │
                              ▼
                Render  https://<api>.onrender.com
                              │
                              ▼
                    Neon Postgres (SSL)
```

Vì `/api/*` đi qua rewrite của Vercel nên trình duyệt luôn thấy **cùng một nguồn**:
không cần CORS, không phải sửa `web/src/lib/api.ts`, và `vnp_ReturnUrl` nằm chung
tên miền với giao diện — đúng bài học đã gặp khi chạy ở máy với hai cổng khác nhau.

> **Làm đúng thứ tự.** Vercel cần biết URL của Render, còn Render cần biết URL của
> Vercel (cho VNPay). Thứ tự dưới đây gỡ được vòng phụ thuộc đó.

---

## Bước 1 — Cơ sở dữ liệu Neon

1. Tạo tài khoản ở <https://neon.tech>, tạo project `cinebook`, chọn vùng **Singapore**
   cho gần Việt Nam.
2. Bấm **Connect**, copy chuỗi kết nối rồi đổi sang dạng Npgsql hiểu được:

```bash
python3 tools/neon-connstring.py 'postgresql://...dán vào đây...'
```

Npgsql **không đọc được** dạng URI mà Neon đưa, nó cần dạng `Khoá=Giá trị;`. Script
cũng bỏ luôn hậu tố `-pooler` trong tên máy chủ: pooler của Neon chạy chế độ
transaction nên không giữ được prepared statement mà EF Core hay dùng, trong khi
ứng dụng .NET đã có sẵn bể kết nối của Npgsql rồi.

Kết quả có dạng:

```
Host=ep-xxx.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=...;SSL Mode=Require;Trust Server Certificate=true
```

> Tên cơ sở dữ liệu mặc định của Neon là **`neondb`**, không phải `cinebook` — đừng sửa tay.

Không cần tự chạy migration: API bật `Database:AutoMigrate` nên tự áp lúc khởi động,
và `DbSeeder` có chốt `if (Users.Any())` nên dữ liệu mẫu chỉ tạo đúng một lần.

## Bước 2 — Sinh khoá bí mật

Đừng dùng lại khoá trong `appsettings.Development.json`.

```bash
openssl rand -base64 32   # cho Jwt__Key
openssl rand -base64 32   # cho Qr__Secret
openssl rand -base64 24   # cho Payment__Mock__Secret
```

> Đổi `Qr__Secret` sẽ làm **mọi mã QR đã phát trước đó không quét được nữa**, vì chữ ký
> vé được ký bằng khoá này (BR-11). Chọn khoá xong thì giữ nguyên.

## Bước 3 — API lên Render

1. <https://render.com> → **New** → **Web Service** → nối repo GitHub.
2. Cấu hình:

   | Mục | Giá trị |
   |---|---|
   | Language | **Docker** |
   | Branch | `main` |
   | Region | **Singapore** — cùng vùng Neon |
   | **Root Directory** | **để trống** — `Dockerfile` nằm ngay gốc repo |
   | Instance Type | **Free** ($0, 0.1 CPU, 512 MB) |
   | Dockerfile Path | `./Dockerfile` *(trong mục **Advanced**)* |
   | Health Check Path | `/api/health` *(trong mục **Advanced**)* |

3. Đặt biến môi trường (Environment):

```
ConnectionStrings__Default = <chuỗi Neon ở bước 1>
Jwt__Key                   = <khoá vừa sinh>
Qr__Secret                 = <khoá vừa sinh>
Payment__Mock__Secret      = <khoá vừa sinh>
Payment__VnPay__TmnCode    = <TmnCode sandbox>
Payment__VnPay__HashSecret = <HashSecret sandbox>
```

Chưa đặt `Payment__VnPay__PublicBaseUrl` và `AppBaseUrl` vội — bước 5 mới có URL Vercel.

4. Deploy xong, mở `https://<api>.onrender.com/api/health`, phải thấy `{"status":"ok",...}`.

**Không cần đặt `PORT`** — Render tự tiêm, `Program.cs` đã đọc biến đó.

## Bước 4 — Giao diện lên Vercel

1. Sửa `web/vercel.json`, thay cả **4 chỗ** `https://THAY-BANG-URL-RENDER-CUA-BAN.invalid`
   bằng URL Render thật rồi commit.

   > Vercel **không** thay biến môi trường trong phần `rewrites`, nên URL bắt buộc phải
   > viết thẳng vào file. Sửa thiếu một dòng là phần đó gọi API hỏng.
   >
   > ⚠️ Chỗ giữ sẵn cố ý đặt đuôi `.invalid` — đây là tên miền cấp cao dành riêng theo
   > RFC 2606, **bảo đảm không bao giờ phân giải được**. Nếu quên thay, yêu cầu sẽ chết
   > ngay tại chỗ. Tuyệt đối không dùng một tên `.onrender.com` nghe hợp lý làm chỗ giữ
   > sẵn: tên miền phụ của Render là duy nhất toàn cầu, rất dễ trúng dịch vụ của người
   > khác đang chạy thật, và toàn bộ lời gọi kể cả `POST /api/auth/login` mang mật khẩu
   > sẽ bay sang máy chủ lạ.

2. <https://vercel.com> → **Add New Project** → chọn repo.
3. Cấu hình:

   | Mục | Giá trị |
   |---|---|
   | Framework Preset | Vite |
   | **Root Directory** | **để trống** |
   | Build Command | `npm run build` (mặc định) |
   | Output Directory | `dist` (mặc định) |

   > Repo `CineBook` **chính là** dự án vite — `package.json` và `vite.config.ts` nằm
   > ngay ở gốc. Điền `web` vào Root Directory sẽ hỏng vì trong repo đó không có
   > thư mục nào tên `web`.

   `vite.config.ts` tự xuất ra `dist` khi thấy biến `VERCEL`, còn build ở máy thì vẫn
   xuất thẳng vào `wwwroot` như cũ.

4. Deploy xong, mở `https://cine-book-mocha.vercel.app` — trang chủ phải hiện phim, tức là
   rewrite `/api` đã thông.

## Bước 5 — Nối VNPay vào tên miền thật

1. Về Render, thêm hai biến rồi deploy lại:

```
Payment__VnPay__PublicBaseUrl = https://cine-book-mocha.vercel.app
Payment__VnPay__AppBaseUrl    = https://cine-book-mocha.vercel.app
```

2. Khai báo trên cổng sandbox VNPay:

   - **Return URL** — `https://cine-book-mocha.vercel.app/api/payments/vnpay-return`
   - **IPN URL** — `https://cine-book-mocha.vercel.app/api/payments/vnpay-ipn`

**Đây là phần đáng nói khi bảo vệ.** Ở máy cá nhân, máy chủ VNPay không gọi được IPN
vào `localhost` nên phải để đường quay lại ghi nhận thanh toán. Lên môi trường thật,
IPN chạy đúng như lý thuyết: **IPN mới là nguồn sự thật** (BR-07), còn đường quay lại
chỉ để hiển thị. Cả hai cùng đi qua `PaymentService.HandleCallbackAsync` nên gửi trùng
cũng chỉ ghi nhận một lần (BR-08).

## Bước 6 — Kiểm tra sau khi lên

```bash
APP=https://cine-book-mocha.vercel.app

curl -s $APP/api/health                      # {"status":"ok"}
curl -s $APP/api/movies | head -c 200        # có dữ liệu phim
curl -s $APP/api/payments/providers          # phải thấy cả Mock và VNPay
curl -s -o /dev/null -w '%{http_code}\n' $APP/api/duong-dan-sai   # 404

python3 tests/test_business_flow.py  $APP
python3 tests/test_race_condition.py $APP
```

Hai file test nhận `base_url` làm tham số nên chạy thẳng lên bản deploy được.
Chúng cần cơ sở dữ liệu **chưa có suất chiếu chồng giờ** ở phòng đầu tiên, nên chạy
ngay sau lần seed đầu tiên là chuẩn nhất.

## Máy ngủ đông

Gói Render miễn phí cho máy **ngủ sau 15 phút** không ai truy cập, đánh thức mất
khoảng **50 giây**.

> **Chỉ giữ máy thức vào những ngày cần trình bày.** Neon free tier giới hạn giờ
> compute mỗi tháng, và Neon chỉ ngủ khi 5 phút liền không có query nào. Trước đây
> job dọn đơn chạy mỗi phút, cộng với việc giữ Render thức, làm Neon thức 24/7
> (khoảng 6 CU-giờ mỗi ngày) và hết quota sau chừng hai tuần rưỡi — API không khởi
> động được nữa. Nay mặc định `KeepAwake:Enabled = false` và job chạy 30 phút một
> lần (`Booking:ExpireJobIntervalMinutes`), nhưng vẫn đừng để giữ thức cả tháng.

Trước buổi bảo vệ khoảng một ngày thì bật các cách dưới đây, xong thì tắt lại:

1. Trên Render đặt `KeepAwake__Enabled = true` — API tự gọi chính nó mỗi 10 phút
   để không bị ngủ (không đánh thức được máy đã ngủ).
2. `.github/workflows/keep-api-awake.yml` gọi `/api/health` mỗi 10 phút, đánh thức
   được cả máy đã ngủ. Vào repo → Settings → Secrets and variables → Actions →
   **Variables** → thêm `API_URL` = `https://<api>.onrender.com`. Không dùng nữa thì
   Actions → "Giữ API thức" → **Disable workflow**.
3. Hoặc <https://cron-job.org> (miễn phí, chạy đúng giờ hơn lịch của GitHub Actions).
4. **Trước buổi bảo vệ vẫn nên mở trang trước vài phút.** Lịch cron nào cũng có thể trễ.

## Sự cố hay gặp

| Hiện tượng | Nguyên nhân |
|---|---|
| Vercel hiện trang nhưng không có phim | `vercel.json` còn URL Render mẫu, hoặc API đang ngủ |
| Gọi `/api/...` trả về HTML | Thiếu rewrite, hoặc Root Directory chưa đặt là `web` |
| Render báo deploy thất bại, log ghi "no open ports" | Có ai đó ghi đè `ASPNETCORE_URLS` chặn cứng một cổng |
| Thanh toán xong quay về trang lỗi | `Payment__VnPay__AppBaseUrl` chưa trỏ tên miền Vercel |
| Vé cũ báo `VE_GIA` sau khi deploy | `Qr__Secret` đã bị đổi, chữ ký vé cũ không còn khớp |
| Trang chủ Render hiện dòng chữ thay vì giao diện | Đúng như thiết kế — giao diện nằm trên Vercel |
| API chết lúc khởi động, log ghi `53000: Your account or project has exceeded the quota` | Neon hết giờ compute của tháng — chờ sang kỳ mới, nâng gói, hoặc tạo project Neon mới rồi đổi `ConnectionStrings__Default`. Xem mục "Máy ngủ đông" để khỏi bị lại |
