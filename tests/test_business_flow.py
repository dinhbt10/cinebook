"""
Kiểm thử nghiệp vụ đầu-cuối: đăng nhập 4 vai trò, tạo suất chiếu, chặn suất chồng giờ,
giữ ghế, chống đặt trùng, giới hạn 8 ghế, combo và mã giảm giá, thanh toán qua webhook
(gửi hai lần để thử chống trùng), sinh vé QR, soát vé, phân quyền và báo cáo.

Chạy:  python3 tests/test_business_flow.py [base_url]
"""
import json, urllib.request, urllib.parse, datetime, sys

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5099"
def call(method, path, body=None, token=None, expect=None):
    url = BASE + path
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    if data: req.add_header("Content-Type", "application/json")
    if token: req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as r:
            txt = r.read().decode()
            return r.status, (json.loads(txt) if txt else None)
    except urllib.error.HTTPError as e:
        txt = e.read().decode()
        try: return e.code, json.loads(txt)
        except: return e.code, txt

ok = fail = 0
def check(label, cond, detail=""):
    global ok, fail
    if cond: ok += 1; print(f"  ✓ {label}" + (f" — {detail}" if detail else ""))
    else:    fail += 1; print(f"  ✗ {label} — {detail}")

print("\n=== 1. Đăng nhập bốn vai trò ===")
tokens = {}
for email, role in [("admin@cinebook.vn","Admin"),("manager@cinebook.vn","CinemaManager"),
                    ("staff@cinebook.vn","Staff"),("khach@gmail.com","Customer")]:
    s, b = call("POST","/api/auth/login",{"email":email,"password":"123456"})
    tokens[role] = b["accessToken"] if s==200 else None
    check(f"{email}", s==200 and b["user"]["role"]==role, f"vai trò {b['user']['role'] if s==200 else b}")

print("\n=== 2. Admin tạo suất chiếu bắt đầu sau 25 phút ===")
s, cinemas = call("GET","/api/cinemas")
cid = cinemas[0]["id"]
s, rooms = call("GET",f"/api/cinemas/{cid}/rooms")
rid = rooms[0]["id"]
s, movies = call("GET","/api/movies?status=NowShowing")
mid = movies[0]["id"]
starts = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(minutes=25))
s, r = call("POST","/api/showtimes",
    {"movieId":mid,"roomId":rid,"startsAtUtc":starts.strftime("%Y-%m-%dT%H:%M:%SZ"),
     "basePrice":75000,"subtitleMode":"Subtitled","openForSale":True}, tokens["Admin"])
check("tạo suất chiếu", s==200, f"HTTP {s}")
showtime_id = r if isinstance(r,str) else (r.get("value") if isinstance(r,dict) else None)
if not showtime_id: print("   ->", r); sys.exit(1)

print("\n=== 3. BR-16: tạo suất chồng giờ trong cùng phòng phải bị chặn ===")
s, r = call("POST","/api/showtimes",
    {"movieId":mid,"roomId":rid,"startsAtUtc":(starts+datetime.timedelta(minutes=10)).strftime("%Y-%m-%dT%H:%M:%SZ"),
     "basePrice":75000,"subtitleMode":"Subtitled","openForSale":True}, tokens["Admin"])
check("chặn suất chồng giờ", s==409 and r.get("code")=="SHOWTIME_OVERLAP", r.get("message",""))

print("\n=== 4. Sơ đồ ghế ===")
s, sm = call("GET", f"/api/showtimes/{showtime_id}/seatmap")
seats = sm["seats"]
avail = [x for x in seats if x["status"]=="Available"]
check("sơ đồ ghế có dữ liệu", s==200 and len(seats)>0, f"{len(seats)} ghế, {len(avail)} trống")
prices = sorted({x["price"] for x in seats})
check("BR-18: giá khác nhau theo loại ghế", len(prices)>1, f"các mức giá: {[f'{p:,.0f}' for p in prices]}")
picked = [x["seatId"] for x in avail[:2]]
picked_labels = [x["label"] for x in avail[:2]]

print("\n=== 5. Khách A giữ ghế ===")
s, bookA = call("POST","/api/bookings/hold",
    {"showtimeId":showtime_id,"seatIds":picked,"combos":[],"promotionCode":None}, tokens["Customer"])
check("giữ ghế thành công", s==200, f"ghế {picked_labels}, còn {bookA.get('secondsLeft')}s" if s==200 else str(bookA))
check("BR-02: đồng hồ đếm ngược ~8 phút", s==200 and 470 <= bookA["secondsLeft"] <= 480, f"{bookA.get('secondsLeft')}s")

print("\n=== 6. BR-01: khách B giành ĐÚNG hai ghế đó — phải bị từ chối ===")
call("POST","/api/auth/register",{"email":"khachb@gmail.com","password":"123456","fullName":"Trần Thị B","phone":None})
s, b2 = call("POST","/api/auth/login",{"email":"khachb@gmail.com","password":"123456"})
tokenB = b2["accessToken"]
s, r = call("POST","/api/bookings/hold",
    {"showtimeId":showtime_id,"seatIds":picked,"combos":[],"promotionCode":None}, tokenB)
check("chặn đặt trùng ghế", s==409 and r.get("code")=="SEATS_TAKEN", r.get("message",""))

print("\n=== 7. Khách B chọn ghế khác — phải thành công ===")
other = [x["seatId"] for x in avail[2:4]]
s, bookB = call("POST","/api/bookings/hold",
    {"showtimeId":showtime_id,"seatIds":other,"combos":[],"promotionCode":None}, tokenB)
check("ghế khác vẫn đặt được", s==200, f"tổng {bookB['total']:,.0f}đ" if s==200 else str(bookB))

print("\n=== 8. BR-04: chọn quá 8 ghế ===")
s, r = call("POST","/api/bookings/hold",
    {"showtimeId":showtime_id,"seatIds":[x["seatId"] for x in avail[10:20]],"combos":[],"promotionCode":None}, tokenB)
check("chặn quá 8 ghế", s==400 and r.get("code")=="TOO_MANY_SEATS", r.get("message",""))

print("\n=== 9. Bắp nước + mã giảm giá ===")
s, combos = call("GET","/api/combos")
s, bookC = call("POST","/api/bookings/hold",
    {"showtimeId":showtime_id,"seatIds":[x["seatId"] for x in avail[4:6]],
     "combos":[{"comboId":combos[0]["id"],"quantity":2}],"promotionCode":"CHAOBAN"}, tokenB)
if s==200:
    check("cộng tiền bắp nước", bookC["concessionSubtotal"]>0, f"{bookC['concessionSubtotal']:,.0f}đ")
    check("áp mã giảm giá CHAOBAN", bookC["discountAmount"]>0, f"giảm {bookC['discountAmount']:,.0f}đ")
    check("tổng tiền = ghế + bắp - giảm",
          abs(bookC["total"] - (bookC["seatsSubtotal"]+bookC["concessionSubtotal"]-bookC["discountAmount"]))<1,
          f"{bookC['total']:,.0f}đ")
else:
    check("đặt kèm combo", False, str(bookC))

print("\n=== 10. Thanh toán ===")
s, pay = call("POST", f"/api/bookings/{bookA['id']}/payments",
              {"provider":"Mock","returnUrl":"/booking/done"}, tokens["Customer"])
check("tạo giao dịch", s==200, pay.get("providerTxnId","") if s==200 else str(pay))
txn = pay["providerTxnId"]

print("\n=== 11. BR-08: cổng gửi webhook HAI lần — chỉ ghi nhận một lần ===")
s1, r1 = call("POST", f"/mock-gateway/notify?txn={urllib.parse.quote(txn)}&status=success")
s2, r2 = call("POST", f"/mock-gateway/notify?txn={urllib.parse.quote(txn)}&status=success")
check("webhook lần 1 ghi nhận", s1==200 and "Đã ghi nhận" in r1.get("message",""), r1.get("message",""))
check("webhook lần 2 bị bỏ qua", s2==200 and "trước đó" in r2.get("message",""), r2.get("message",""))

s, bk = call("GET", f"/api/bookings/{bookA['id']}", token=tokens["Customer"])
check("đơn hàng chuyển sang PAID", bk["status"]=="Paid", f"trạng thái {bk['status']}")

print("\n=== 12. Vé và mã QR ===")
s, tickets = call("GET", f"/api/bookings/{bookA['id']}/tickets", token=tokens["Customer"])
check("sinh vé cho từng ghế", len(tickets)==len(picked), f"{len(tickets)} vé: {[t['code'] for t in tickets]}")
qr = tickets[0]["qrPayload"]
check("BR-11: QR có chữ ký", "." in qr and len(qr.split(".")[1])>20, qr[:34]+"…")

print("\n=== 13. Soát vé tại rạp ===")
s, r = call("POST","/api/checkin/scan",{"qrPayload":qr}, tokens["Staff"])
check("vé hợp lệ được cho vào", r["allowed"] and r["verdict"]=="CHO_VAO", r["message"])
s, r = call("POST","/api/checkin/scan",{"qrPayload":qr}, tokens["Staff"])
check("BR-12: quét lại lần hai bị chặn", not r["allowed"] and r["verdict"]=="DUNG_LAI", r["message"])
s, r = call("POST","/api/checkin/scan",{"qrPayload":"TKFAKE1234.chukygia"}, tokens["Staff"])
check("BR-11: mã QR giả bị từ chối", not r["allowed"] and r["verdict"]=="VE_GIA", r["message"])
s, r = call("POST","/api/checkin/scan",{"qrPayload":qr}, tokens["Customer"])
check("BR-19: khách không được soát vé", s==403, f"HTTP {s}")

print("\n=== 14. Báo cáo ===")
s, dash = call("GET","/api/reports/dashboard", token=tokens["Admin"])
check("dashboard có doanh thu", s==200 and dash["revenueToday"]>0,
      f"doanh thu hôm nay {dash['revenueToday']:,.0f}đ · lấp đầy {dash['occupancyToday']:.2%}")
s, r = call("GET","/api/reports/dashboard", token=tokens["Customer"])
check("khách không xem được báo cáo", s==403, f"HTTP {s}")

print(f"\n{'='*54}\nKẾT QUẢ: {ok} đạt · {fail} lỗi\n{'='*54}")
sys.exit(1 if fail else 0)
