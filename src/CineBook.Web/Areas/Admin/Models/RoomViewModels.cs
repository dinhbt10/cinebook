using System.ComponentModel.DataAnnotations;
using CineBook.Application.Dtos;
using CineBook.Domain.Enums;

namespace CineBook.Web.Areas.Admin.Models;

public class CinemaRoomsViewModel
{
    public CinemaDto Cinema { get; set; } = null!;
    public List<RoomDto> Rooms { get; set; } = new List<RoomDto>();
}

public class RoomsIndexViewModel
{
    public List<CinemaRoomsViewModel> Cinemas { get; set; } = new List<CinemaRoomsViewModel>();
    public CreateRoomViewModel Form { get; set; } = new CreateRoomViewModel();
    public bool OpenForm { get; set; }
}

public class CreateRoomViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn rạp.")]
    [Display(Name = "Rạp")]
    public Guid? CinemaId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên phòng.")]
    [StringLength(64, ErrorMessage = "Tên phòng tối đa 64 ký tự.")]
    [Display(Name = "Tên phòng")]
    public string Name { get; set; } = "";

    [Display(Name = "Loại phòng")]
    public RoomType RoomType { get; set; } = RoomType.TwoD;

    [Required(ErrorMessage = "Vui lòng nhập số hàng ghế.")]
    [Range(1, 26, ErrorMessage = "Số hàng ghế phải từ 1 đến 26.")]
    [Display(Name = "Số hàng ghế")]
    public int? RowCount { get; set; } = 8;

    [Required(ErrorMessage = "Vui lòng nhập số ghế mỗi hàng.")]
    [Range(1, 40, ErrorMessage = "Số ghế mỗi hàng phải từ 1 đến 40.")]
    [Display(Name = "Số ghế mỗi hàng")]
    public int? SeatsPerRow { get; set; } = 12;

    [Required(ErrorMessage = "Vui lòng nhập hàng VIP bắt đầu.")]
    [Range(0, 26, ErrorMessage = "Hàng VIP bắt đầu phải từ 0 đến 26.")]
    [Display(Name = "Hàng VIP từ")]
    public int? VipFromRow { get; set; } = 4;

    [Required(ErrorMessage = "Vui lòng nhập hàng VIP kết thúc.")]
    [Range(0, 26, ErrorMessage = "Hàng VIP kết thúc phải từ 0 đến 26.")]
    [Display(Name = "Hàng VIP đến")]
    public int? VipToRow { get; set; } = 7;

    public int TotalSeats()
    {
        var rows = RowCount ?? 0;
        var seats = SeatsPerRow ?? 0;
        return rows > 1 ? (rows - 1) * seats + Math.Max(1, (int)Math.Floor(seats / 2.0)) : rows * seats;
    }

    public char LastRowLabel()
    {
        return (char)(64 + Math.Max(1, RowCount ?? 0));
    }
}
