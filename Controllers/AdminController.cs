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
        private readonly IAuditLogService _auditLog;

        public AdminController(IAdminService adminService, IAuditLogService auditLog)
        {
            _adminService = adminService;
            _auditLog = auditLog;
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
            await _auditLog.LogAsync("USER_CREATE", "User", null, request.Username,
                new { after = new { request.Username, request.Email, role = request.Role ?? "User" } });
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
            await _auditLog.LogAsync("SETTING_UPDATE", "Setting", key, key, new { after = new { value = request.Value } });
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
            await _auditLog.LogAsync("JOB_CLEANUP_STUCK", "UploadJob", null, "Dọn job bị kẹt",
                new { stuckMinutes = request?.StuckMinutes ?? 15, result });
            return Ok(result);
        }

        /// <summary>
        /// API Cho phép Admin kích hoạt chạy lại (Retry) một job bất kỳ
        /// </summary>
        [HttpPost("jobs/{id}/retry")]
        public async Task<IActionResult> RetryJob(string id)
        {
            var success = await _adminService.RetryJobAsync(id);
            await _auditLog.LogAsync("JOB_RETRY", "UploadJob", id, id);
            return Ok(new { success, message = "Đã lên lịch chạy lại Job thành công." });
        }

        /// <summary>
        /// API Cho phép Admin xóa một job khỏi hệ thống
        /// </summary>
        [HttpDelete("jobs/{id}")]
        public async Task<IActionResult> DeleteJob(string id)
        {
            var success = await _adminService.DeleteJobAsync(id);
            await _auditLog.LogAsync("JOB_DELETE", "UploadJob", id, id);
            return Ok(new { success, message = "Đã xóa Job thành công." });
        }

        [HttpGet("voices")]
        public async Task<IActionResult> GetVoices()
        {
            var voices = await _adminService.GetVoicesAsync();
            return Ok(voices);
        }

        [HttpPost("voices")]
        public async Task<IActionResult> CreateVoice([FromBody] UpsertTtsVoiceRequest request)
        {
            var voice = await _adminService.CreateVoiceAsync(request);
            await _auditLog.LogAsync("VOICE_CREATE", "TtsVoice", voice.Id, voice.Name,
                new { after = new { voice.VoiceId, voice.Name, voice.Gender, voice.IsActive, voice.IsDefault } });
            return Created($"/api/admin/voices/{voice.Id}", voice);
        }

        [HttpPut("voices/{id}")]
        public async Task<IActionResult> UpdateVoice(string id, [FromBody] UpsertTtsVoiceRequest request)
        {
            var voice = await _adminService.UpdateVoiceAsync(id, request);
            await _auditLog.LogAsync("VOICE_UPDATE", "TtsVoice", voice.Id, voice.Name,
                new { after = new { voice.VoiceId, voice.Name, voice.Gender, voice.IsActive, voice.IsDefault } });
            return Ok(voice);
        }

        [HttpDelete("voices/{id}")]
        public async Task<IActionResult> DeleteVoice(string id)
        {
            var success = await _adminService.DeleteVoiceAsync(id);
            await _auditLog.LogAsync("VOICE_DELETE", "TtsVoice", id, id);
            return Ok(new { success, message = "Đã xóa giọng đọc thành công." });
        }

        [HttpPost("voices/{id}/generate-sample")]
        public async Task<IActionResult> GenerateVoiceSample(string id)
        {
            var voice = await _adminService.GenerateVoiceSampleAsync(id);
            return Ok(voice);
        }

        [HttpPost("voices/generate-samples")]
        public async Task<IActionResult> GenerateMissingVoiceSamples()
        {
            var count = await _adminService.GenerateMissingVoiceSamplesAsync();
            return Ok(new { generatedCount = count });
        }
    }
}
