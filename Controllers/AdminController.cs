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
        public async Task<IActionResult> GetUsageReport([FromQuery] DateTime? date)
        {
            var report = await _adminService.GetUsageReportAsync(date);
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
    }
}
