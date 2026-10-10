using CineBook.Application.Dtos;

namespace CineBook.Web.Models;

public class CinemaCardViewModel
{
    public CinemaDto Cinema { get; set; } = null!;
    public List<RoomDto> Rooms { get; set; } = new();
}

public class VoucherListViewModel
{
    public List<VoucherDto> Vouchers { get; set; } = new();
    public bool CanSave { get; set; }
}
