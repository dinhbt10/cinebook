# ---------- tầng build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Chép riêng các file .csproj trước rồi mới restore: Docker lưu đệm được tầng này,
# nên sửa code mà không đổi phụ thuộc thì lần build sau bỏ qua hẳn bước tải gói.
COPY CineBook.sln .
COPY src/CineBook.Domain/CineBook.Domain.csproj                 src/CineBook.Domain/
COPY src/CineBook.Application/CineBook.Application.csproj       src/CineBook.Application/
COPY src/CineBook.Infrastructure/CineBook.Infrastructure.csproj src/CineBook.Infrastructure/
COPY src/CineBook.Api/CineBook.Api.csproj                       src/CineBook.Api/
RUN dotnet restore src/CineBook.Api/CineBook.Api.csproj

COPY src/ src/
RUN dotnet publish src/CineBook.Api/CineBook.Api.csproj \
    -c Release -o /app --no-restore /p:UseAppHost=false

# ---------- tầng chạy ----------
# Ảnh aspnet nhẹ hơn sdk khoảng 4 lần vì không mang theo trình biên dịch.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Render tiêm biến PORT lúc chạy; 8080 chỉ là giá trị dự phòng khi chạy Docker tay.
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080

ENTRYPOINT ["dotnet", "CineBook.Api.dll"]
