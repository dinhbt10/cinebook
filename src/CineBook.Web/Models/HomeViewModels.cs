using CineBook.Application.Dtos;

namespace CineBook.Web.Models;

public class HomeViewModel
{
    public List<MovieListItemDto> NowShowing { get; set; } = new();
    public List<MovieListItemDto> Spotlight { get; set; } = new();
    public int CinemaCount { get; set; }
    public List<ShowtimeListItemDto> TodayShowtimes { get; set; } = new();
    public List<PromotionDto> Promotions { get; set; } = new();
}

public class MovieGridViewModel
{
    public List<MovieListItemDto> Movies { get; set; } = new();
    public int Total { get; set; }
    public int PageNo { get; set; }
    public int PageCount { get; set; }
    public bool HasFilter { get; set; }
}

public class GenreItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public class MovieDetailViewModel
{
    public MovieDetailDto Movie { get; set; } = null!;
    public List<DateOnly> Days { get; set; } = new();
    public DateOnly SelectedDate { get; set; }
    public List<ShowtimeListItemDto> Showtimes { get; set; } = new();
}
