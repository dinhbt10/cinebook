#!/usr/bin/env python3
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
