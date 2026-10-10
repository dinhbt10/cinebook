using System.ComponentModel.DataAnnotations;
using CineBook.Application.Dtos;
using CineBook.Domain.Enums;

namespace CineBook.Web.Areas.Admin.Models;

public class ShowtimesPageViewModel
{
    public DateOnly Date { get; set; }
    public Guid? CinemaId { get; set; }
    public List<CinemaDto> Cinemas { get; set; } = new List<CinemaDto>();
    public List<ShowtimeListItemDto> Showtimes { get; set; } = new List<ShowtimeListItemDto>();
    public ShowtimeFormViewModel Form { get; set; } = new ShowtimeFormViewModel();
    public bool OpenCreate { get; set; }
}

public class ShowtimeFormViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn phim.")]
    [Display(Name = "Phim")]
    public Guid? MovieId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn rạp.")]
    [Display(Name = "Rạp")]
    public Guid? CinemaId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phòng chiếu.")]
    [Display(Name = "Phòng chiếu")]
    public Guid? RoomId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Giờ bắt đầu (giờ Việt Nam)")]
    public DateTime? StartsLocal { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giá cơ sở.")]
    [Range(1000, 10000000, ErrorMessage = "Giá cơ sở phải từ 1.000đ trở lên.")]
    [Display(Name = "Giá cơ sở (đồng)")]
    public decimal? BasePrice { get; set; } = 75000;

    [Display(Name = "Ngôn ngữ")]
    public SubtitleMode SubtitleMode { get; set; } = SubtitleMode.Subtitled;

    [Required(ErrorMessage = "Vui lòng nhập giá ghế thường.")]
    [Range(1000, 10000000, ErrorMessage = "Giá vé phải từ 1.000đ trở lên.")]
    [Display(Name = "Ghế thường")]
    public decimal? PriceStandard { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giá ghế VIP.")]
    [Range(1000, 10000000, ErrorMessage = "Giá vé phải từ 1.000đ trở lên.")]
    [Display(Name = "Ghế VIP")]
    public decimal? PriceVip { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giá ghế đôi.")]
    [Range(1000, 10000000, ErrorMessage = "Giá vé phải từ 1.000đ trở lên.")]
    [Display(Name = "Ghế đôi")]
    public decimal? PriceCouple { get; set; }

    public string? PriceOverrides { get; set; }

    [Display(Name = "Mở bán vé ngay sau khi tạo")]
    public bool OpenForSale { get; set; } = true;

    public DateOnly? FilterDate { get; set; }

    public Guid? FilterCinemaId { get; set; }
}

public class CancelShowtimeViewModel
{
    [Required]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Cần nhập lý do hủy suất chiếu.")]
    [StringLength(500, ErrorMessage = "Lý do hủy tối đa 500 ký tự.")]
    [Display(Name = "Lý do hủy (khách sẽ thấy)")]
    public string Reason { get; set; } = "Sự cố kỹ thuật";

    public DateOnly? FilterDate { get; set; }

    public Guid? FilterCinemaId { get; set; }
}
