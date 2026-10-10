using CineBook.Application.Dtos;

namespace CineBook.Web.Areas.Admin.Models;

public class DashboardViewModel
{
    public DashboardDto Stats { get; set; } = null!;
    public List<MovieRevenueDto> TopMovies { get; set; } = new List<MovieRevenueDto>();
}

public class ReportsViewModel
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public List<RevenuePointDto> ByDay { get; set; } = new List<RevenuePointDto>();
    public List<MovieRevenueDto> ByMovie { get; set; } = new List<MovieRevenueDto>();
    public decimal TotalTicket { get; set; }
    public decimal TotalCombo { get; set; }
}
