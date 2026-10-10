using CineBook.Application.Dtos;
using CineBook.Application.Services;
using CineBook.Domain.Enums;
using CineBook.Infrastructure.Persistence;
using CineBook.Web.Common;
using CineBook.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Web.Controllers;

public class MoviesController : Controller
{
    private const int PageSize = 10;

    private readonly CineBookDbContext _context;
    private readonly ShowtimeService _showtimes;

    public MoviesController(CineBookDbContext context, ShowtimeService showtimes)
    {
        _context = context;
        _showtimes = showtimes;
    }

    public async Task<IActionResult> Index()
    {
        var model = await SearchAsync(null, null, null, 0);

        ViewBag.Genres = await (from g in _context.Genres
                                orderby g.Name
                                select new GenreItem { Id = g.Id, Name = g.Name })
                               .ToListAsync();
        ViewBag.pageCount = model.PageCount;
        return View(model);
    }

    public async Task<IActionResult> Search(string? q, MovieStatus? status, Guid? genreId)
    {
        var model = await SearchAsync(q, status, genreId, 0);
        return PartialView("_MovieResults", model);
    }

    public async Task<IActionResult> LoadMore(string? q, MovieStatus? status, Guid? genreId, int pageNo)
    {
        var model = await SearchAsync(q, status, genreId, pageNo);
        return PartialView("_MovieGrid", model.Movies);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var movie = await _context.Movies
            .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFound");
        }

        var today = Fmt.VnToday;
        var model = new MovieDetailViewModel();
        model.Movie = new MovieDetailDto(movie.Id, movie.Title, movie.OriginalTitle, movie.Description,
            movie.DurationMinutes, movie.AgeRating.ToString(), movie.Director, movie.Cast, movie.Language,
            movie.PosterUrl, movie.BackdropUrl, movie.TrailerUrl, movie.ReleaseDate, movie.Status.ToString(),
            movie.MovieGenres.Select(g => g.Genre.Name), movie.Country, movie.EndDate,
            movie.MovieGenres.Select(g => g.GenreId));
        model.SelectedDate = today;
        model.Days = Enumerable.Range(0, 7).Select(i => today.AddDays(i)).ToList();
        model.Showtimes = (await _showtimes.SearchAsync(id, null, today)).ToList();
        return View(model);
    }

    public async Task<IActionResult> Showtimes(Guid id, DateOnly date)
    {
        var showtimes = await _showtimes.SearchAsync(id, null, date);
        return PartialView("_Showtimes", showtimes.ToList());
    }

    private async Task<MovieGridViewModel> SearchAsync(string? q, MovieStatus? status, Guid? genreId, int pageNo)
    {
        var query = _context.Movies.Where(m => m.Status != MovieStatus.Draft);

        if (status != null)
            query = query.Where(m => m.Status == status);
        if (genreId != null)
            query = query.Where(m => m.MovieGenres.Any(mg => mg.GenreId == genreId));
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(m => EF.Functions.ILike(m.Title, "%" + q.Trim() + "%"));

        var total = await query.CountAsync();

        var model = new MovieGridViewModel();
        model.Total = total;
        model.PageNo = pageNo;
        model.PageCount = (int)Math.Ceiling(total / (double)PageSize);
        model.HasFilter = !string.IsNullOrEmpty(q) || status != null || genreId != null;
        model.Movies = await query
            .OrderByDescending(m => m.ReleaseDate)
            .ThenBy(m => m.Id)
            .Skip(pageNo * PageSize)
            .Take(PageSize)
            .Select(m => new MovieListItemDto(m.Id, m.Title, m.DurationMinutes, m.AgeRating.ToString(),
                m.PosterUrl, m.BackdropUrl, m.TrailerUrl, m.ReleaseDate, m.Status.ToString(),
                m.MovieGenres.Select(g => g.Genre.Name)))
            .ToListAsync();
        return model;
    }
}
