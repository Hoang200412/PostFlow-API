using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using PostFlow.Services.Interfaces;

namespace PostFlow.Filters
{
    /// <summary>
    /// Gắn lên endpoint gọi AI: trừ 1 lượt hạn mức ngày của user trước khi chạy action.
    /// Hết lượt → trả HTTP 429 với code AI_QUOTA_EXCEEDED. Action ném lỗi hoặc trả 4xx/5xx → hoàn lại lượt.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class RequireAiQuotaAttribute : Attribute, IAsyncActionFilter
    {
        public const string ExceededCode = "AI_QUOTA_EXCEEDED";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var quota = context.HttpContext.RequestServices.GetRequiredService<IAiQuotaService>();
            var consume = await quota.TryConsumeAsync(userId);

            if (!consume.Allowed)
            {
                var status = consume.Status;
                context.Result = new ObjectResult(new
                {
                    statusCode = StatusCodes.Status429TooManyRequests,
                    code = ExceededCode,
                    message = "Bạn đã dùng hết lượt AI trong ngày. Hạn mức sẽ được làm mới lúc 00:00 (giờ Việt Nam).",
                    plan = status.Plan,
                    limit = status.Limit + status.Bonus,
                    used = status.Used,
                    resetAt = status.ResetAt,
                    timestamp = DateTime.UtcNow
                })
                {
                    StatusCode = StatusCodes.Status429TooManyRequests
                };
                return;
            }

            var executed = await next();

            var failed = (executed.Exception != null && !executed.ExceptionHandled)
                || (executed.Result is IStatusCodeActionResult { StatusCode: >= 400 });
            if (failed)
            {
                await quota.RefundAsync(userId);
            }
        }
    }
}
