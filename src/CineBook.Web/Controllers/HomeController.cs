using CineBook.Application.Dtos;
using CineBook.Application.Services;
using CineBook.Domain.Enums;
using CineBook.Infrastructure.Persistence;
using CineBook.Web.Common;
using CineBook.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Web.Controllers;

public class HomeController : Controller
{
    private readonly CineBookDbContext _context;
    private readonly ShowtimeService _showtimes;

    public HomeController(CineBookDbContext context, ShowtimeService showtimes)
    {
        _context = context;
        _showtimes = showtimes;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeViewModel();
        model.NowShowing = await GetMoviesAsync(MovieStatus.NowShowing);
        model.Spotlight = model.NowShowing.Take(3).ToList();
        model.CinemaCount = await _context.Cinemas.CountAsync(c => c.IsActive);
        model.TodayShowtimes = (await _showtimes.SearchAsync(null, null, Fmt.VnToday)).ToList();

        var now = DateTime.UtcNow;
        model.Promotions = await (from p in _context.Promotions
                                  where p.IsActive && p.StartsAtUtc <= now && p.EndsAtUtc >= now
                                  orderby p.EndsAtUtc
                                  select new PromotionDto(p.Id, p.Code, p.Name, p.DiscountType.ToString(),
                                      p.Value, p.MaxDiscount, p.MinOrderAmount, p.EndsAtUtc))
                                 .ToListAsync();

        ViewBag.Notice = TempData["Notice"];
        return View(model);
    }

    public async Task<IActionResult> MovieGrid(MovieStatus status = MovieStatus.NowShowing)
    {
        var movies = await GetMoviesAsync(status);
        return PartialView("_MovieGrid", movies);
    }

    public IActionResult Error()
    {
        return View();
    }

    private Task<List<MovieListItemDto>> GetMoviesAsync(MovieStatus status)
    {
        return _context.Movies
            .Where(m => m.Status == status)
            .OrderByDescending(m => m.ReleaseDate)
            .Select(m => new MovieListItemDto(m.Id, m.Title, m.DurationMinutes, m.AgeRating.ToString(),
                m.PosterUrl, m.BackdropUrl, m.TrailerUrl, m.ReleaseDate, m.Status.ToString(),
                m.MovieGenres.Select(g => g.Genre.Name)))
            .ToListAsync();
    }
}
