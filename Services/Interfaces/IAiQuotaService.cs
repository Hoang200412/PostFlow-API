using System;
using System.Threading.Tasks;

namespace PostFlow.Services.Interfaces
{
    /// <summary>Trạng thái hạn mức gọi AI trong ngày của một người dùng.</summary>
    /// <param name="Plan">Gói dịch vụ hiện tại (Trial / Pro).</param>
    /// <param name="Limit">Hạn mức gốc trong ngày; -1 nghĩa là không giới hạn.</param>
    /// <param name="Bonus">Số lượt admin tặng thêm cho riêng hôm nay.</param>
    /// <param name="Used">Số lượt đã dùng hôm nay.</param>
    /// <param name="Remaining">Số lượt còn lại; -1 nghĩa là không giới hạn.</param>
    /// <param name="IsUnlimited">true nếu không bị giới hạn.</param>
    /// <param name="ResetAt">Thời điểm (UTC) hạn mức được làm mới, tức 00:00 giờ Việt Nam hôm sau.</param>
    public sealed record AiQuotaStatus(
        string Plan,
        int Limit,
        int Bonus,
        int Used,
        int Remaining,
        bool IsUnlimited,
        DateTime ResetAt);

    public sealed record AiQuotaConsumeResult(bool Allowed, AiQuotaStatus Status);

    public interface IAiQuotaService
    {
        /// <summary>Lấy trạng thái hạn mức hôm nay của user.</summary>
        Task<AiQuotaStatus> GetStatusAsync(string userId);

        /// <summary>
        /// Trừ 1 lượt gọi AI một cách atomic. Trả về Allowed=false (không trừ) nếu user đã dùng hết hạn mức hôm nay.
        /// </summary>
        Task<AiQuotaConsumeResult> TryConsumeAsync(string userId);

        /// <summary>Hoàn lại 1 lượt (dùng khi lần gọi AI thất bại không do người dùng).</summary>
        Task RefundAsync(string userId);
    }
}
