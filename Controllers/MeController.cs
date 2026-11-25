using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    /// <summary>Thông tin tài khoản của chính người đang đăng nhập.</summary>
    [Authorize]
    [ApiController]
    [Route("api/me")]
    public class MeController : ControllerBase
    {
        private readonly IAiQuotaService _aiQuotaService;

        public MeController(IAiQuotaService aiQuotaService)
        {
            _aiQuotaService = aiQuotaService;
        }

        /// <summary>
        /// API Lấy gói dịch vụ và hạn mức gọi AI còn lại trong ngày (làm mới 00:00 giờ Việt Nam)
        /// </summary>
        [HttpGet("ai-quota")]
        public async Task<IActionResult> GetAiQuota()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var status = await _aiQuotaService.GetStatusAsync(userId);
            return Ok(status);
        }
    }
}
