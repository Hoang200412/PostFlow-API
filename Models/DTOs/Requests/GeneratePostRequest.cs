using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests;

public class GeneratePostRequest
{
    [Required(ErrorMessage = "Mô tả bài viết không được để trống.")]
    [StringLength(1000, ErrorMessage = "Mô tả bài viết không được vượt quá 1000 ký tự.")]
    public string Prompt { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nền tảng không được để trống.")]
    [StringLength(50, ErrorMessage = "Nền tảng không hợp lệ.")]
    public string Platform { get; set; } = "facebook";

    [Required(ErrorMessage = "Tone bài viết không được để trống.")]
    [StringLength(200, ErrorMessage = "Tone bài viết không được vượt quá 200 ký tự.")]
    public string Tone { get; set; } = "thân thiện, dễ đọc";

    [Required(ErrorMessage = "Phong cách hình ảnh không được để trống.")]
    [StringLength(100, ErrorMessage = "Phong cách hình ảnh không hợp lệ.")]
    public string ImageStyle { get; set; } = "realistic";

    [Required(ErrorMessage = "Tỷ lệ hình ảnh không được để trống.")]
    [StringLength(20, ErrorMessage = "Tỷ lệ hình ảnh không hợp lệ.")]
    public string AspectRatio { get; set; } = "1:1";

    [Required(ErrorMessage = "Tông màu không được để trống.")]
    [StringLength(100, ErrorMessage = "Tông màu không được vượt quá 100 ký tự.")]
    public string ColorTone { get; set; } = "tự nhiên, sáng rõ";

    [Required(ErrorMessage = "Cảm xúc hình ảnh không được để trống.")]
    [StringLength(100, ErrorMessage = "Cảm xúc hình ảnh không được vượt quá 100 ký tự.")]
    public string Mood { get; set; } = "tích cực";
    public bool IncludeTextInImage { get; set; }

    [Url(ErrorMessage = "Ảnh tham khảo phải là URL hợp lệ.")]
    public string? ReferenceImageUrl { get; set; }
}
