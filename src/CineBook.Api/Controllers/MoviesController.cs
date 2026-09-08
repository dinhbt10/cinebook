using CineBook.Api.Common;
using CineBook.Application.Abstractions;
using CineBook.Application.Dtos;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Api.Controllers;

[ApiController]
[Route("api/movies")]
public class MoviesController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, [FromQuery] MovieStatus? status, [FromQuery] Guid? genreId,
        CancellationToken ct = default)
    {
        var query = db.Movies.AsNoTracking()
            .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
            .Where(m => m.Status != MovieStatus.Draft);

        if (status is { } s) query = query.Where(m => m.Status == s);
        if (genreId is { } gid) query = query.Where(m => m.MovieGenres.Any(mg => mg.GenreId == gid));
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(m => EF.Functions.ILike(m.Title, $"%{q.Trim()}%"));

        var items = await query
            .OrderByDescending(m => m.ReleaseDate)
            .Select(m => new MovieListItemDto(m.Id, m.Title, m.DurationMinutes, m.AgeRating.ToString(),
                m.PosterUrl, m.BackdropUrl, m.TrailerUrl, m.ReleaseDate, m.Status.ToString(),
                m.MovieGenres.Select(g => g.Genre.Name)))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var m = await db.Movies.AsNoTracking()
            .Include(x => x.MovieGenres).ThenInclude(mg => mg.Genre)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (m is null) return NotFound(new { message = "Không tìm thấy phim." });

        return Ok(new MovieDetailDto(m.Id, m.Title, m.OriginalTitle, m.Description, m.DurationMinutes,
            m.AgeRating.ToString(), m.Director, m.Cast, m.Language, m.PosterUrl, m.BackdropUrl,
            m.TrailerUrl, m.ReleaseDate, m.Status.ToString(), m.MovieGenres.Select(g => g.Genre.Name)));
    }

    [HttpGet("/api/genres")]
    public async Task<IActionResult> Genres(CancellationToken ct) =>
        Ok(await db.Genres.AsNoTracking().OrderBy(g => g.Name)
            .Select(g => new { g.Id, g.Name, g.Slug }).ToListAsync(ct));

    // ---------------- quản trị ----------------

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Create(SaveMovieRequest req, CancellationToken ct)
    {
        var movie = new Movie();
        Apply(movie, req);
        db.Movies.Add(movie);
        await SetGenresAsync(movie, req.GenreIds, ct);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Detail), new { id = movie.Id }, new { movie.Id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Update(Guid id, SaveMovieRequest req, CancellationToken ct)
    {
        var movie = await db.Movies.Include(m => m.MovieGenres)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (movie is null) return NotFound();

        Apply(movie, req);
        movie.UpdatedAtUtc = DateTime.UtcNow;
        db.MovieGenres.RemoveRange(movie.MovieGenres);
        await SetGenresAsync(movie, req.GenreIds, ct);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var movie = await db.Movies.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (movie is null) return NotFound();

        // Phim đã có suất chiếu thì chỉ đánh dấu kết thúc, không xoá — giữ toàn vẹn dữ liệu bán vé.
        if (await db.Showtimes.AnyAsync(s => s.MovieId == id, ct))
        {
            movie.Status = MovieStatus.Ended;
            await db.SaveChangesAsync(ct);
            return Ok(new { message = "Phim đã có suất chiếu nên được chuyển sang trạng thái kết thúc." });
        }

        db.Movies.Remove(movie);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void Apply(Movie m, SaveMovieRequest r)
    {
        m.Title = r.Title.Trim();
        m.OriginalTitle = r.OriginalTitle;
        m.Description = r.Description;
        m.DurationMinutes = r.DurationMinutes;
        m.ReleaseDate = r.ReleaseDate;
        m.EndDate = r.EndDate;
        m.AgeRating = r.AgeRating;
        m.Director = r.Director;
        m.Cast = r.Cast;
        m.Language = r.Language;
        m.Country = r.Country;
        m.PosterUrl = r.PosterUrl;
        m.BackdropUrl = r.BackdropUrl;
        m.TrailerUrl = r.TrailerUrl;
        m.Status = r.Status;
    }

    private async Task SetGenresAsync(Movie movie, IEnumerable<Guid>? genreIds, CancellationToken ct)
    {
        if (genreIds is null) return;
        var ids = genreIds.Distinct().ToList();
        var valid = await db.Genres.Where(g => ids.Contains(g.Id)).Select(g => g.Id).ToListAsync(ct);
        foreach (var gid in valid)
            db.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = gid });
    }
}
