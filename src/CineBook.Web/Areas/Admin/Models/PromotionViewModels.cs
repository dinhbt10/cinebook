using System.ComponentModel.DataAnnotations;
using CineBook.Application.Dtos;
using CineBook.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CineBook.Web.Areas.Admin.Models;

public class PromotionsIndexViewModel
{
    public List<AdminPromotionDto> Items { get; set; } = new List<AdminPromotionDto>();
    public PromotionFormViewModel? Form { get; set; }
}

public class PromotionFormViewModel
{
    [BindNever]
    public Guid? Id { get; set; }

    [BindNever]
    public string? OriginalCode { get; set; }

    [BindNever]
    public bool Locked { get; set; }

    [BindNever]
    public int UsedCount { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã voucher.")]
    [RegularExpression("[A-Za-z0-9_-]{3,32}", ErrorMessage = "Mã dài 3–32 ký tự, chỉ gồm chữ không dấu, số, gạch ngang hoặc gạch dưới.")]
    [Display(Name = "Mã voucher")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
    [StringLength(128, ErrorMessage = "Tên hiển thị tối đa 128 ký tự.")]
    [Display(Name = "Tên hiển thị")]
    public string Name { get; set; } = "";

    [Display(Name = "Loại giảm")]
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

    [Required(ErrorMessage = "Vui lòng nhập mức giảm.")]
    [Range(1, 1000000000, ErrorMessage = "Mức giảm phải lớn hơn 0.")]
    [Display(Name = "Mức giảm")]
    public decimal? Value { get; set; } = 10;

    [Range(1000, 1000000000, ErrorMessage = "Giảm tối đa phải từ 1.000đ trở lên.")]
    [Display(Name = "Giảm tối đa (đồng)")]
    public decimal? MaxDiscount { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giá trị đơn tối thiểu.")]
    [Range(0, 1000000000, ErrorMessage = "Giá trị đơn tối thiểu không được âm.")]
    [Display(Name = "Đơn tối thiểu (đồng)")]
    public decimal? MinOrderAmount { get; set; } = 0;

    [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Bắt đầu (giờ Việt Nam)")]
    public DateTime? StartsLocal { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian kết thúc.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Kết thúc (giờ Việt Nam)")]
    public DateTime? EndsLocal { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Tổng lượt dùng phải từ 1 trở lên, để trống nếu không giới hạn.")]
    [Display(Name = "Tổng lượt dùng")]
    public int? UsageLimit { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số lượt mỗi khách.")]
    [Range(1, int.MaxValue, ErrorMessage = "Số lượt mỗi khách phải từ 1 trở lên.")]
    [Display(Name = "Số lượt mỗi khách")]
    public int? PerUserLimit { get; set; } = 1;

    [Display(Name = "Đang áp dụng")]
    public bool IsActive { get; set; } = true;

    public bool IsEditing => Id != null;

    public bool IsPercent => DiscountType == DiscountType.Percentage;
}
