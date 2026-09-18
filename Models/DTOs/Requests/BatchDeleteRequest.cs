using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests
{
    public class BatchDeleteRequest
    {
        [Required(ErrorMessage = "Danh sách ID video không được để trống.")]
        [MinLength(1, ErrorMessage = "Cần chọn ít nhất 1 video để xóa.")]
        public List<string> Ids { get; set; } = new();
    }
}
