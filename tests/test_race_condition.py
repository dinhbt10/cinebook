"""
Kiểm thử đồng thời cho BR-01: 20 người bấm CÙNG một ghế trong cùng một khoảnh khắc.
Chỉ đúng MỘT người được giữ ghế, 19 người còn lại phải nhận lỗi SEATS_TAKEN.

Chạy:  python3 tests/test_race_condition.py [base_url]
"""
import json, sys, urllib.request, datetime
from concurrent.futures import ThreadPoolExecutor

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5099"
N = 20

def call(method, path, body=None, token=None):
    req = urllib.request.Request(BASE + path,
                                 data=json.dumps(body).encode() if body is not None else None,
                                 method=method)
    if body is not None: req.add_header("Content-Type", "application/json")
    if token: req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as r:
            t = r.read().decode(); return r.status, (json.loads(t) if t else None)
    except urllib.error.HTTPError as e:
        t = e.read().decode()
        try: return e.code, json.loads(t)
        except Exception: return e.code, t

def login(email, pw="123456"):
    s, b = call("POST", "/api/auth/login", {"email": email, "password": pw})
    return b["accessToken"]

# --- chuẩn bị: N tài khoản và một suất chiếu mới tinh ---
print(f"Chuẩn bị {N} tài khoản…")
tokens = []
for i in range(N):
    email = f"race{i}@test.vn"
    call("POST", "/api/auth/register",
         {"email": email, "password": "123456", "fullName": f"Người thử {i}", "phone": None})
    tokens.append(login(email))

admin = login("admin@cinebook.vn")
cid = call("GET", "/api/cinemas")[1][0]["id"]
rid = call("GET", f"/api/cinemas/{cid}/rooms")[1][-1]["id"]
mid = call("GET", "/api/movies?status=NowShowing")[1][0]["id"]
starts = datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=3, hours=5)

s, showtime_id = call("POST", "/api/showtimes",
    {"movieId": mid, "roomId": rid, "startsAtUtc": starts.strftime("%Y-%m-%dT%H:%M:%SZ"),
     "basePrice": 75000, "subtitleMode": "Subtitled", "openForSale": True}, admin)
if s != 200:
    print("Không tạo được suất chiếu:", showtime_id); sys.exit(1)

seatmap = call("GET", f"/api/showtimes/{showtime_id}/seatmap")[1]
target = next(x for x in seatmap["seats"] if x["status"] == "Available")
print(f"Mục tiêu: ghế {target['label']} của suất chiếu vừa tạo\n")

# --- tất cả cùng bấm một lúc ---
def grab(token):
    return call("POST", "/api/bookings/hold",
                {"showtimeId": showtime_id, "seatIds": [target["seatId"]],
                 "combos": [], "promotionCode": None}, token)

print(f"{N} người bấm giữ ghế {target['label']} cùng lúc…")
with ThreadPoolExecutor(max_workers=N) as pool:
    results = list(pool.map(grab, tokens))

won    = [r for r in results if r[0] == 200]
lost   = [r for r in results if r[0] == 409 and r[1].get("code") == "SEATS_TAKEN"]
others = [r for r in results if r not in won and r not in lost]

print(f"  Giữ được ghế : {len(won)}")
print(f"  Bị từ chối   : {len(lost)}  (SEATS_TAKEN)")
print(f"  Kết quả khác : {len(others)}")
for r in others[:3]: print("   ->", r)

# --- kiểm chứng lại trên sơ đồ ghế ---
after = call("GET", f"/api/showtimes/{showtime_id}/seatmap")[1]
state = next(x for x in after["seats"] if x["seatId"] == target["seatId"])

ok = len(won) == 1 and len(lost) == N - 1 and state["status"] == "Held"
print(f"\nTrạng thái ghế sau cuộc đua: {state['status']}")
print("=" * 52)
print("KẾT QUẢ:", "ĐẠT — đúng một người giữ được ghế" if ok else "LỖI — có bán trùng ghế!")
print("=" * 52)
sys.exit(0 if ok else 1)
