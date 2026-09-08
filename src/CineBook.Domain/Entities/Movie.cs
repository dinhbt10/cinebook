using CineBook.Domain.Common;
using CineBook.Domain.Enums;

namespace CineBook.Domain.Entities;

public class Movie : BaseEntity
{
    public string Title { get; set; } = null!;
    public string? OriginalTitle { get; set; }
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public AgeRating AgeRating { get; set; }
    public string? Director { get; set; }
    public string? Cast { get; set; }
    public string? Language { get; set; }
    public string? Country { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public MovieStatus Status { get; set; } = MovieStatus.Draft;

    public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}

public class Genre : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
}

public class MovieGenre
{
    public Guid MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public Guid GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}
