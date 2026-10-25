using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        /// <summary>
        /// API Lấy danh sách người dùng trong hệ thống (Dành cho Admin)
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _adminService.GetUsersAsync();
            return Ok(users);
        }

        /// <summary>
        /// API Tạo người dùng mới thủ công (Dành cho Admin)
        /// </summary>
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            var result = await _adminService.CreateUserAsync(request);
            return Created("/api/admin/users", result);
        }

        /// <summary>
        /// API Xem báo cáo mức độ sử dụng và chi phí API của người dùng theo ngày
        /// </summary>
        [HttpGet("usage")]
        public async Task<IActionResult> GetUsageReport([FromQuery] GetUsageReportRequest request)
        {
            var report = await _adminService.GetUsageReportAsync(request);
            return Ok(report);
        }

        /// <summary>
        /// API Cập nhật cấu hình cài đặt hệ thống (System Settings)
        /// </summary>
        [HttpPut("settings/{key}")]
        public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingRequest request)
        {
            var setting = await _adminService.UpdateSettingAsync(key, request);
            return Ok(setting);
        }

        /// <summary>
        /// API Lấy danh sách tất cả các tiến trình đăng video trong toàn hệ thống (Dành cho Admin)
        /// </summary>
        [HttpGet("jobs")]
        public async Task<IActionResult> GetJobs([FromQuery] AdminJobQueryParams queryParams)
        {
            var result = await _adminService.GetPublishingJobsAsync(queryParams);
            return Ok(result);
        }

        /// <summary>
        /// API Quét và tự động dọn dẹp các job bị treo Processing quá lâu (Zombie jobs)
        /// </summary>
        [HttpPost("jobs/cleanup-stuck")]
        public async Task<IActionResult> CleanupStuckJobs([FromQuery] CleanupStuckJobsRequest request)
        {
            var result = await _adminService.CleanupStuckJobsAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// API Cho phép Admin kích hoạt chạy lại (Retry) một job bất kỳ
        /// </summary>
        [HttpPost("jobs/{id}/retry")]
        public async Task<IActionResult> RetryJob(string id)
        {
            var success = await _adminService.RetryJobAsync(id);
            return Ok(new { success, message = "Đã lên lịch chạy lại Job thành công." });
        }

        /// <summary>
        /// API Cho phép Admin xóa một job khỏi hệ thống
        /// </summary>
        [HttpDelete("jobs/{id}")]
        public async Task<IActionResult> DeleteJob(string id)
        {
            var success = await _adminService.DeleteJobAsync(id);
            return Ok(new { success, message = "Đã xóa Job thành công." });
        }
    }
}
