using System.ComponentModel.DataAnnotations;
using CineBook.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace CineBook.Web.Areas.Admin.Models;

public class MovieFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên phim.")]
    [StringLength(256, ErrorMessage = "Tên phim tối đa 256 ký tự.")]
    [Display(Name = "Tên phim")]
    public string Title { get; set; } = "";

    [StringLength(256, ErrorMessage = "Tên gốc tối đa 256 ký tự.")]
    [Display(Name = "Tên gốc")]
    public string? OriginalTitle { get; set; }

    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập thời lượng phim.")]
    [Range(1, 600, ErrorMessage = "Thời lượng phải từ 1 đến 600 phút.")]
    [Display(Name = "Thời lượng (phút)")]
    public int? DurationMinutes { get; set; } = 100;

    [Required(ErrorMessage = "Vui lòng chọn ngày khởi chiếu.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày khởi chiếu")]
    public DateTime? ReleaseDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Ngày kết thúc chiếu")]
    public DateTime? EndDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn nhãn phân loại tuổi.")]
    [Display(Name = "Nhãn phân loại tuổi")]
    public AgeRating? AgeRating { get; set; } = Domain.Enums.AgeRating.T13;

    [StringLength(256, ErrorMessage = "Tên đạo diễn tối đa 256 ký tự.")]
    [Display(Name = "Đạo diễn")]
    public string? Director { get; set; }

    [Display(Name = "Diễn viên")]
    public string? Cast { get; set; }

    [StringLength(100, ErrorMessage = "Ngôn ngữ tối đa 100 ký tự.")]
    [Display(Name = "Ngôn ngữ")]
    public string? Language { get; set; } = "Tiếng Việt";

    [StringLength(100, ErrorMessage = "Quốc gia tối đa 100 ký tự.")]
    [Display(Name = "Quốc gia")]
    public string? Country { get; set; } = "Việt Nam";

    [Display(Name = "Ảnh poster")]
    public string? PosterUrl { get; set; }

    [Display(Name = "Tải ảnh poster")]
    public IFormFile? PosterFile { get; set; }

    [Display(Name = "Ảnh nền")]
    public string? BackdropUrl { get; set; }

    [Display(Name = "Trailer")]
    public string? TrailerUrl { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    [Display(Name = "Trạng thái")]
    public MovieStatus? Status { get; set; } = MovieStatus.NowShowing;

    [Display(Name = "Thể loại")]
    public List<Guid> GenreIds { get; set; } = new List<Guid>();
}
