using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Application.Services;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CineBook.Infrastructure.Persistence;

public class DbSeeder(
    CineBookDbContext db,
    IPasswordHasher hasher,
    IQrSigner qrSigner,
    ILogger<DbSeeder> logger)
{
    public const string DefaultPassword = "123456";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            logger.LogInformation("Cơ sở dữ liệu đã có dữ liệu, bỏ qua bước tạo dữ liệu mẫu.");
            return;
        }

        logger.LogInformation("Đang tạo dữ liệu mẫu…");

        var cgvHaNoi = new Cinema
        {
            Name = "CineBook Cầu Giấy", City = "Hà Nội", District = "Cầu Giấy",
            Address = "Tầng 5, Toà nhà Discovery, 302 Cầu Giấy", Hotline = "1900 6017"
        };
        var cinemaHcm = new Cinema
        {
            Name = "CineBook Nguyễn Trãi", City = "TP. Hồ Chí Minh", District = "Quận 5",
            Address = "Tầng 4, 180 Nguyễn Trãi", Hotline = "1900 6017"
        };
        db.Cinemas.AddRange(cgvHaNoi, cinemaHcm);

        var rooms = new List<Room>
        {
            new() { CinemaId = cgvHaNoi.Id, Name = "Phòng 1", RoomType = RoomType.TwoD },
            new() { CinemaId = cgvHaNoi.Id, Name = "Phòng 2", RoomType = RoomType.ThreeD },
            new() { CinemaId = cgvHaNoi.Id, Name = "Phòng IMAX", RoomType = RoomType.Imax },
            new() { CinemaId = cinemaHcm.Id, Name = "Phòng 1", RoomType = RoomType.TwoD }
        };
        db.Rooms.AddRange(rooms);

        foreach (var room in rooms)
        {
            var rowCount = room.RoomType == RoomType.Imax ? 10 : 8;
            var perRow = room.RoomType == RoomType.Imax ? 16 : 12;
            db.Seats.AddRange(RoomService.BuildSeats(room.Id, rowCount, perRow, 4, 7));
        }

        db.Users.AddRange(
            NewUser("admin@cinebook.vn", "Quản trị hệ thống", UserRole.Admin),
            NewUser("manager@cinebook.vn", "Quản lý rạp Cầu Giấy", UserRole.CinemaManager, cgvHaNoi.Id),
            NewUser("staff@cinebook.vn", "Nhân viên soát vé", UserRole.Staff, cgvHaNoi.Id),
            NewUser("khach@gmail.com", "Nguyễn Văn Khách", UserRole.Customer));

        var genres = new[] { "Hành động", "Hài", "Kinh dị", "Tình cảm", "Hoạt hình", "Khoa học viễn tưởng" }
            .Select(n => new Genre { Name = n, Slug = Slugify(n) }).ToArray();
        db.Genres.AddRange(genres);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        Movie[] movies =
        {
            NewMovie("Mưa Đỏ", 124, AgeRating.T16, today.AddDays(-14), MovieStatus.NowShowing,
                "Bộ phim chiến tranh tái hiện 81 ngày đêm bảo vệ Thành cổ Quảng Trị.", genres[0]),
            NewMovie("Chuyện Nhà Mình", 108, AgeRating.P, today.AddDays(-7), MovieStatus.NowShowing,
                "Câu chuyện hài hước và ấm áp về ba thế hệ trong một gia đình Hà Nội.", genres[1]),
            NewMovie("Đêm Không Trăng", 96, AgeRating.T18, today.AddDays(-3), MovieStatus.NowShowing,
                "Một ngôi làng nhỏ và những đêm không ai dám ra khỏi nhà.", genres[2]),
            NewMovie("Hành Tinh Thứ Chín", 141, AgeRating.T13, today.AddDays(-1), MovieStatus.NowShowing,
                "Chuyến thám hiểm vượt ngoài rìa hệ Mặt Trời để tìm một hành tinh chưa từng được thấy.", genres[5]),
            NewMovie("Cún Con Và Biển Cả", 88, AgeRating.P, today.AddDays(10), MovieStatus.ComingSoon,
                "Hành trình của chú chó nhỏ đi tìm chủ dọc bờ biển miền Trung.", genres[4])
        };

        db.PriceRules.AddRange(
            new PriceRule { Name = "Ghế VIP", SeatType = SeatType.Vip, Multiplier = 1.25m, Priority = 10 },
            new PriceRule { Name = "Ghế đôi", SeatType = SeatType.Couple, Multiplier = 2.0m, Priority = 10 },
            new PriceRule { Name = "Phòng 3D", RoomType = RoomType.ThreeD, Surcharge = 20000m, Priority = 20 },
            new PriceRule { Name = "Phòng IMAX", RoomType = RoomType.Imax, Multiplier = 1.4m, Priority = 20 },
            new PriceRule { Name = "Suất sáng trước 12h", TimeFrom = new TimeOnly(0, 0), TimeTo = new TimeOnly(12, 0), Multiplier = 0.7m, Priority = 30 },
            new PriceRule { Name = "Cuối tuần — thứ bảy", DayOfWeek = 6, Multiplier = 1.2m, Priority = 40 },
            new PriceRule { Name = "Cuối tuần — chủ nhật", DayOfWeek = 0, Multiplier = 1.2m, Priority = 40 });

        db.Combos.AddRange(
            new Combo { Name = "Combo 1 — 1 bắp lớn + 1 nước", Price = 89000m, Description = "Bắp ngọt 60oz và 1 nước ngọt 32oz" },
            new Combo { Name = "Combo 2 — 1 bắp lớn + 2 nước", Price = 109000m, Description = "Dành cho hai người" },
            new Combo { Name = "Combo gia đình", Price = 189000m, Description = "2 bắp lớn, 4 nước ngọt và 1 snack" },
            new Combo { Name = "Nước ngọt 32oz", Price = 35000m });

        var nowUtc = DateTime.UtcNow;
        db.Promotions.AddRange(
            new Promotion
            {
                Code = "CHAOBAN", Name = "Chào bạn mới — giảm 20%",
                DiscountType = DiscountType.Percentage, Value = 20m, MaxDiscount = 50000m,
                MinOrderAmount = 100000m, StartsAtUtc = nowUtc.AddDays(-1), EndsAtUtc = nowUtc.AddMonths(3),
                UsageLimit = 1000, PerUserLimit = 1
            },
            new Promotion
            {
                Code = "THU3VUI", Name = "Thứ ba vui vẻ — giảm 30.000đ",
                DiscountType = DiscountType.FixedAmount, Value = 30000m,
                MinOrderAmount = 150000m, StartsAtUtc = nowUtc.AddDays(-1), EndsAtUtc = nowUtc.AddMonths(3),
                PerUserLimit = 5
            });

        await db.SaveChangesAsync(ct);

        await SeedShowtimesAsync(movies.Where(m => m.Status == MovieStatus.NowShowing).ToList(), rooms, ct);
        await SeedBookingsAsync(ct);

        logger.LogInformation("Đã tạo dữ liệu mẫu. Mật khẩu mọi tài khoản: {Password}", DefaultPassword);
    }

    private async Task SeedShowtimesAsync(List<Movie> movies, List<Room> rooms, CancellationToken ct)
    {
        var rules = await db.PriceRules.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Priority).ToListAsync(ct);
        var pricing = new PricingService(db);
        var allSeats = await db.Seats.AsNoTracking().Where(s => s.IsActive).ToListAsync(ct);
        var seatsByRoom = allSeats.GroupBy(s => s.RoomId).ToDictionary(g => g.Key, g => g.ToList());
        var cinemaByRoom = rooms.ToDictionary(r => r.Id, r => r.CinemaId);
        var typeByRoom = rooms.ToDictionary(r => r.Id, r => r.RoomType);

        int[] slots = [9, 12, 15, 18, 21];
        var todayLocal = DateTime.UtcNow.AddHours(PricingService.VietnamUtcOffsetHours).Date;

        for (var day = -7; day < 7; day++)
        {
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (!seatsByRoom.TryGetValue(room.Id, out var seats)) continue;

                var slotCount = day < 0 ? 3 : slots.Length;

                for (var s = 0; s < slotCount; s++)
                {
                    var movie = movies[((day + i + s) % movies.Count + movies.Count) % movies.Count];
                    var startsLocal = todayLocal.AddDays(day).AddHours(slots[s]);
                    var startsUtc = startsLocal.AddHours(-PricingService.VietnamUtcOffsetHours);
                    if (day >= 0 && startsUtc <= DateTime.UtcNow.AddMinutes(30)) continue;

                    var basePrice = room.RoomType switch
                    {
                        RoomType.Imax => 120000m,
                        RoomType.ThreeD => 90000m,
                        _ => 75000m
                    };

                    var showtime = new Showtime
                    {
                        MovieId = movie.Id,
                        RoomId = room.Id,
                        StartsAtUtc = startsUtc,
                        EndsAtUtc = startsUtc.AddMinutes(movie.DurationMinutes + 15),
                        BasePrice = basePrice,
                        SubtitleMode = SubtitleMode.Subtitled,
                        IsOnSale = true
                    };
                    db.Showtimes.Add(showtime);

                    foreach (var seat in seats)
                    {
                        db.ShowtimeSeats.Add(new ShowtimeSeat
                        {
                            ShowtimeId = showtime.Id,
                            SeatId = seat.Id,
                            Status = SeatStatus.Available,
                            Price = pricing.CalculateSeatPrice(
                                basePrice, startsUtc, cinemaByRoom[room.Id],
                                typeByRoom[room.Id], seat.SeatType, rules)
                        });
                    }
                }
            }
            await db.SaveChangesAsync(ct);
        }
    }

    // Bổ sung suất chiếu từ hôm nay tới hết ngày lastDayLocal (giờ Việt Nam) cho các phim đang chiếu.
    // Khung giờ nào trong phòng đã có suất (chưa hủy) chồng lên thì bỏ qua, nên chạy lại nhiều lần vẫn an toàn.
    public async Task<int> SeedShowtimesUntilAsync(DateOnly lastDayLocal, CancellationToken ct = default)
    {
        var movies = await db.Movies.AsNoTracking()
            .Where(m => m.Status == MovieStatus.NowShowing)
            .OrderBy(m => m.Title).ToListAsync(ct);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.CinemaId).ThenBy(r => r.Name).ToListAsync(ct);
        if (movies.Count == 0 || rooms.Count == 0)
        {
            logger.LogWarning("Không có phim đang chiếu hoặc phòng chiếu, không tạo suất chiếu.");
            return 0;
        }

        var rules = await db.PriceRules.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Priority).ToListAsync(ct);
        var pricing = new PricingService(db);
        var allSeats = await db.Seats.AsNoTracking().Where(s => s.IsActive).ToListAsync(ct);
        var seatsByRoom = allSeats.GroupBy(s => s.RoomId).ToDictionary(g => g.Key, g => g.ToList());

        int[] slots = [9, 12, 15, 18, 21];
        var nowUtc = DateTime.UtcNow;
        var todayLocal = nowUtc.AddHours(PricingService.VietnamUtcOffsetHours).Date;
        var lastLocal = lastDayLocal.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var fromUtc = todayLocal.AddHours(-PricingService.VietnamUtcOffsetHours);
        var toUtc = lastLocal.AddDays(1).AddHours(-PricingService.VietnamUtcOffsetHours);

        var existing = await db.Showtimes.AsNoTracking()
            .Where(s => !s.IsCancelled && s.EndsAtUtc > fromUtc && s.StartsAtUtc < toUtc)
            .Select(s => new { s.RoomId, s.StartsAtUtc, s.EndsAtUtc })
            .ToListAsync(ct);
        var busy = existing.GroupBy(s => s.RoomId)
            .ToDictionary(g => g.Key, g => g.Select(s => (s.StartsAtUtc, s.EndsAtUtc)).ToList());

        var created = 0;
        for (var date = todayLocal; date <= lastLocal; date = date.AddDays(1))
        {
            var day = (date - todayLocal).Days;
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (!seatsByRoom.TryGetValue(room.Id, out var seats)) continue;
                if (!busy.TryGetValue(room.Id, out var roomBusy)) busy[room.Id] = roomBusy = [];

                for (var s = 0; s < slots.Length; s++)
                {
                    var movie = movies[(day + i + s) % movies.Count];
                    var startsUtc = date.AddHours(slots[s] - PricingService.VietnamUtcOffsetHours);
                    var endsUtc = startsUtc.AddMinutes(movie.DurationMinutes + 15);
                    if (startsUtc <= nowUtc.AddMinutes(30)) continue;
                    if (roomBusy.Any(b => b.StartsAtUtc < endsUtc && startsUtc < b.EndsAtUtc)) continue;

                    var basePrice = room.RoomType switch
                    {
                        RoomType.Imax => 120000m,
                        RoomType.ThreeD => 90000m,
                        _ => 75000m
                    };

                    var showtime = new Showtime
                    {
                        MovieId = movie.Id,
                        RoomId = room.Id,
                        StartsAtUtc = startsUtc,
                        EndsAtUtc = endsUtc,
                        BasePrice = basePrice,
                        SubtitleMode = SubtitleMode.Subtitled,
                        IsOnSale = true
                    };
                    db.Showtimes.Add(showtime);
                    roomBusy.Add((startsUtc, endsUtc));
                    created++;

                    foreach (var seat in seats)
                    {
                        db.ShowtimeSeats.Add(new ShowtimeSeat
                        {
                            ShowtimeId = showtime.Id,
                            SeatId = seat.Id,
                            Status = SeatStatus.Available,
                            Price = pricing.CalculateSeatPrice(
                                basePrice, startsUtc, room.CinemaId, room.RoomType, seat.SeatType, rules)
                        });
                    }
                }
            }
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Đã tạo {Count} suất chiếu đến hết ngày {Last}", created, lastDayLocal);
        return created;
    }

    private async Task SeedBookingsAsync(CancellationToken ct)
    {
        var rnd = new Random(20260908);
        var now = DateTime.UtcNow;

        string[] names = [
            "Lê Văn An", "Phạm Thị Bình", "Trần Quốc Cường", "Ngô Thị Dung",
            "Vũ Minh Đức", "Đỗ Thu Giang", "Hoàng Văn Hải", "Bùi Thị Hoa",
            "Đặng Quang Huy", "Lý Thị Lan", "Phan Văn Nam", "Trịnh Thị Oanh",
        ];
        for (var i = 0; i < names.Length; i++)
            db.Users.Add(NewUser($"khach{i + 1}@gmail.com", names[i], UserRole.Customer));
        await db.SaveChangesAsync(ct);

        var customers = await db.Users.Where(u => u.Role == UserRole.Customer)
            .Select(u => u.Id).ToListAsync(ct);
        var showtimes = await db.Showtimes.AsNoTracking().ToListAsync(ct);
        var allSeats = await db.ShowtimeSeats.ToListAsync(ct);
        var byShowtime = allSeats.GroupBy(x => x.ShowtimeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => Guid.NewGuid()).ToList());

        var bookingCount = 0;

        foreach (var st in showtimes)
        {
            if (!byShowtime.TryGetValue(st.Id, out var pool)) continue;

            var isPast = st.StartsAtUtc < now;
            var fillRate = isPast ? 0.25 + rnd.NextDouble() * 0.45
                                  : 0.05 + rnd.NextDouble() * 0.22;
            var target = (int)(pool.Count * fillRate);
            var taken = 0;

            while (taken < target)
            {
                var size = Math.Min(rnd.Next(1, 4), target - taken);
                var seats = pool.Skip(taken).Take(size).ToList();
                if (seats.Count == 0) break;
                taken += seats.Count;

                var paidAt = st.StartsAtUtc.AddHours(-(1 + rnd.NextDouble() * 60));
                if (paidAt > now) paidAt = now.AddMinutes(-rnd.Next(5, 600));

                var booking = new Booking
                {
                    Code = CodeGenerator.New("CB"),
                    UserId = customers[rnd.Next(customers.Count)],
                    ShowtimeId = st.Id,
                    Status = BookingStatus.Paid,
                    SeatsSubtotal = seats.Sum(x => x.Price),
                    ConcessionSubtotal = 0m,
                    DiscountAmount = 0m,
                    CreatedAtUtc = paidAt.AddMinutes(-3),
                    HoldExpiresAtUtc = paidAt.AddMinutes(5),
                    PaidAtUtc = paidAt
                };
                booking.Total = booking.SeatsSubtotal;
                db.Bookings.Add(booking);
                bookingCount++;

                foreach (var seat in seats)
                {
                    seat.Status = SeatStatus.Sold;
                    seat.BookingId = booking.Id;
                    seat.HoldExpiresAtUtc = null;

                    var code = CodeGenerator.New("TK");
                    db.Tickets.Add(new Ticket
                    {
                        BookingId = booking.Id,
                        SeatId = seat.SeatId,
                        Code = code,
                        QrSignature = qrSigner.Sign(code),
                        Status = isPast ? TicketStatus.Used : TicketStatus.Valid,
                        CheckedInAtUtc = isPast ? st.StartsAtUtc.AddMinutes(-rnd.Next(5, 40)) : null,
                        CreatedAtUtc = paidAt
                    });
                }

                var payment = new Payment
                {
                    BookingId = booking.Id,
                    Provider = "Mock",
                    ProviderTxnId = $"SEED{Guid.NewGuid():N}"[..24],
                    Amount = booking.Total,
                    Status = PaymentStatus.Succeeded,
                    CompletedAtUtc = paidAt,
                    CreatedAtUtc = paidAt
                };
                db.Payments.Add(payment);
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã tạo {Count} đơn đặt vé mẫu", bookingCount);
    }

    private User NewUser(string email, string name, UserRole role, Guid? cinemaId = null) => new()
    {
        Email = email,
        FullName = name,
        Role = role,
        CinemaId = cinemaId,
        EmailVerified = true,
        PasswordHash = hasher.Hash(DefaultPassword)
    };

    private Movie NewMovie(string title, int minutes, AgeRating rating, DateOnly release,
        MovieStatus status, string description, Genre genre)
    {
        var movie = new Movie
        {
            Title = title,
            DurationMinutes = minutes,
            AgeRating = rating,
            ReleaseDate = release,
            Status = status,
            Description = description,
            Language = "Tiếng Việt",
            Country = "Việt Nam",
            PosterUrl = null
        };
        db.Movies.Add(movie);
        db.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = genre.Id });
        return movie;
    }

    private static string Slugify(string s) =>
        string.Concat(s.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                            != System.Globalization.UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant().Replace('đ', 'd').Replace(' ', '-');
}
