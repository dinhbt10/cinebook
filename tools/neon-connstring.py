#!/usr/bin/env python3
"""
Đổi chuỗi kết nối Neon sang định dạng Npgsql hiểu được.

Npgsql KHÔNG đọc được dạng URI `postgresql://user:pass@host/db` mà Neon đưa cho,
nó cần dạng `Khoá=Giá trị;`. Script này chuyển giúp, và bỏ luôn hậu tố `-pooler`
trong tên máy chủ.

Vì sao bỏ pooler: pooler của Neon chạy chế độ transaction, không giữ được câu lệnh
đã chuẩn bị sẵn (prepared statement) mà EF Core hay dùng. Ứng dụng .NET vốn đã có
sẵn bể kết nối của Npgsql nên nối thẳng vào endpoint trực tiếp là hợp lý và ít lỗi hơn.

Cách dùng:
    python3 tools/neon-connstring.py 'postgresql://...'
hoặc dán vào rồi Enter:
    python3 tools/neon-connstring.py
"""
import sys
import urllib.parse as u

raw = (sys.argv[1] if len(sys.argv) > 1 else input("Dán chuỗi Neon: ")).strip()
p = u.urlparse(raw)

if not p.hostname or not p.username:
    sys.exit("Không đọc được chuỗi. Cần dạng postgresql://user:pass@host/db?sslmode=require")

host = p.hostname.replace("-pooler", "")
db = p.path.lstrip("/") or "neondb"
user = u.unquote(p.username)
pwd = u.unquote(p.password or "")
port = p.port or 5432

print()
print("Dán nguyên dòng dưới đây vào biến ConnectionStrings__Default trên Render:")
print()
print(
    "Host=" + host
    + ";Port=" + str(port)
    + ";Database=" + db
    + ";Username=" + user
    + ";Password=" + pwd
    + ";SSL Mode=Require;Trust Server Certificate=true"
)
print()
