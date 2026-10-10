using CineBook.Application.Dtos;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using CineBook.Infrastructure.Persistence;
using CineBook.Web.Areas.Admin.Models;
using CineBook.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,CinemaManager")]
public class MoviesController : Controller
{
    private static readonly string[] PosterExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxPosterBytes = 2 * 1024 * 1024;

    private readonly CineBookDbContext _context;
    private readonly IWebHostEnvironment _env;

    public MoviesController(CineBookDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        var movies = await _context.Movies
            .OrderByDescending(m => m.ReleaseDate)
            .Select(m => new MovieListItemDto(m.Id, m.Title, m.DurationMinutes, m.AgeRating.ToString(),
                m.PosterUrl, m.BackdropUrl, m.TrailerUrl, m.ReleaseDate, m.Status.ToString(),
                m.MovieGenres.Select(g => g.Genre.Name)))
            .ToListAsync();

        ViewBag.Notice = TempData["Notice"];
        ViewBag.Error = TempData["Error"];
        return View(movies);
    }

    public async Task<IActionResult> Create()
    {
        var model = new MovieFormViewModel();
        model.ReleaseDate = Fmt.VnToday.ToDateTime(TimeOnly.MinValue);
        await LoadFormListsAsync();
        return PartialView("_MovieForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MovieFormViewModel model)
    {
        await SavePosterAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadFormListsAsync();
            return PartialView("_MovieForm", model);
        }

        var movie = new Movie();
        Apply(movie, model);
        _context.Movies.Add(movie);
        await SetGenresAsync(movie, model.GenreIds);
        await _context.SaveChangesAsync();

        return Json(new { redirect = Url.Action("Index") });
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var movie = await _context.Movies
            .Include(m => m.MovieGenres)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movie == null) return NotFound();

        var model = new MovieFormViewModel
        {
            Id = movie.Id,
            Title = movie.Title,
            OriginalTitle = movie.OriginalTitle,
            Description = movie.Description,
            DurationMinutes = movie.DurationMinutes,
            ReleaseDate = movie.ReleaseDate.ToDateTime(TimeOnly.MinValue),
            EndDate = movie.EndDate?.ToDateTime(TimeOnly.MinValue),
            AgeRating = movie.AgeRating,
            Director = movie.Director,
            Cast = movie.Cast,
            Language = movie.Language,
            Country = movie.Country,
            PosterUrl = movie.PosterUrl,
            BackdropUrl = movie.BackdropUrl,
            TrailerUrl = movie.TrailerUrl,
            Status = movie.Status,
            GenreIds = movie.MovieGenres.Select(g => g.GenreId).ToList()
        };

        await LoadFormListsAsync();
        return PartialView("_MovieForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, MovieFormViewModel model)
    {
        var movie = await _context.Movies
            .Include(m => m.MovieGenres)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movie == null) return NotFound();

        model.Id = id;
        await SavePosterAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadFormListsAsync();
            return PartialView("_MovieForm", model);
        }

        Apply(movie, model);
        movie.UpdatedAtUtc = DateTime.UtcNow;
        _context.MovieGenres.RemoveRange(movie.MovieGenres);
        await SetGenresAsync(movie, model.GenreIds);
        await _context.SaveChangesAsync();

        return Json(new { redirect = Url.Action("Index") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == id);
        if (movie == null)
        {
            TempData["Error"] = "Không tìm thấy dữ liệu.";
            return RedirectToAction("Index");
        }

        var hasShowtimes = await _context.Showtimes.AnyAsync(s => s.MovieId == id);
        if (hasShowtimes)
        {
            movie.Status = MovieStatus.Ended;
            await _context.SaveChangesAsync();
            TempData["Notice"] = "Phim đã có suất chiếu nên được chuyển sang trạng thái kết thúc.";
            return RedirectToAction("Index");
        }

        _context.Movies.Remove(movie);
        await _context.SaveChangesAsync();
        TempData["Notice"] = "Đã xoá phim.";
        return RedirectToAction("Index");
    }

    private async Task LoadFormListsAsync()
    {
        ViewBag.Genres = await (from g in _context.Genres
                                orderby g.Name
                                select g).ToListAsync();

        ViewBag.AgeRatings = new List<SelectListItem>
        {
            new SelectListItem("P", "P"),
            new SelectListItem("K", "K"),
            new SelectListItem("T13", "T13"),
            new SelectListItem("T16", "T16"),
            new SelectListItem("T18", "T18"),
        };

        ViewBag.Statuses = new List<SelectListItem>
        {
            new SelectListItem("Bản nháp", "Draft"),
            new SelectListItem("Sắp chiếu", "ComingSoon"),
            new SelectListItem("Đang chiếu", "NowShowing"),
            new SelectListItem("Đã kết thúc", "Ended"),
        };
    }

    private async Task SavePosterAsync(MovieFormViewModel model)
    {
        var file = model.PosterFile;
        if (file == null || file.Length == 0) return;

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!PosterExtensions.Contains(ext))
        {
            ModelState.AddModelError("PosterFile", "Chỉ nhận ảnh JPG, PNG hoặc WEBP.");
            return;
        }
        if (file.Length > MaxPosterBytes)
        {
            ModelState.AddModelError("PosterFile", "Ảnh poster tối đa 2MB.");
            return;
        }
        if (!ModelState.IsValid) return;

        var folder = Path.Combine(_env.WebRootPath, "images", "posters");
        Directory.CreateDirectory(folder);
        var fileName = Guid.NewGuid().ToString("N") + ext;
        using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        model.PosterUrl = "/images/posters/" + fileName;
        ModelState.Remove("PosterUrl");
    }

    private static void Apply(Movie m, MovieFormViewModel r)
    {
        m.Title = r.Title.Trim();
        m.OriginalTitle = Blank(r.OriginalTitle);
        m.Description = RichText.IsHtml(r.Description) ? Blank(RichText.Sanitize(r.Description)) : Blank(r.Description);
        m.DurationMinutes = r.DurationMinutes ?? 0;
        m.ReleaseDate = DateOnly.FromDateTime(r.ReleaseDate!.Value);
        m.EndDate = r.EndDate.HasValue ? DateOnly.FromDateTime(r.EndDate.Value) : null;
        m.AgeRating = r.AgeRating ?? AgeRating.T13;
        m.Director = Blank(r.Director);
        m.Cast = Blank(r.Cast);
        m.Language = r.Language ?? "";
        m.Country = Blank(r.Country);
        m.PosterUrl = Blank(r.PosterUrl);
        m.BackdropUrl = Blank(r.BackdropUrl);
        m.TrailerUrl = Blank(r.TrailerUrl);
        m.Status = r.Status ?? MovieStatus.Draft;
    }

    private static string? Blank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private async Task SetGenresAsync(Movie movie, List<Guid> genreIds)
    {
        var ids = genreIds.Distinct().ToList();
        var valid = await _context.Genres
            .Where(g => ids.Contains(g.Id))
            .Select(g => g.Id)
            .ToListAsync();
        foreach (var gid in valid)
        {
            _context.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = gid });
        }
    }
}
