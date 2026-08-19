using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    public class WebhookController : ControllerBase
    {
        private readonly IWebhookService _webhookService;

        public WebhookController(IWebhookService webhookService)
        {
            _webhookService = webhookService;
        }

        /// <summary>
        /// API Nhận Webhook callback thông báo kết quả đăng bài từ các nền tảng mạng xã hội
        /// </summary>
        [HttpPost("platform-callback")]
        public async Task<IActionResult> ReceiveCallback([FromBody] PlatformCallbackRequest request)
        {
            await _webhookService.ProcessCallbackAsync(request);
            return Ok(new { message = "Nhận webhook thành công." });
        }
    }
}
