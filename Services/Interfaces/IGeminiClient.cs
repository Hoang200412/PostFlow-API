using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PostFlow.Services.Interfaces
{
    /// <summary>Tin nhắn trong lịch sử hội thoại nhiều lượt (Multi-turn conversation).</summary>
    public sealed record GeminiChatMessage(string Role, string Text);

    /// <summary>Tuỳ chọn cho một lần gọi Gemini: vai trò/quy tắc (system), schema đầu ra, nhiệt độ sinh, thinking budget và search grounding.</summary>
    /// <param name="SystemInstruction">Vai trò và quy tắc cố định, tách khỏi dữ liệu người dùng.</param>
    /// <param name="ResponseSchema">Schema JSON (dạng OpenAPI subset của Gemini) ép cấu trúc đầu ra.</param>
    /// <param name="Temperature">Ghi đè nhiệt độ mặc định.</param>
    /// <param name="ThinkingBudget">Số token tối đa dùng để suy luận nháp (0 để tắt reasoning, >0 để bật thinking).</param>
    /// <param name="EnableGoogleSearch">Bật công cụ tra cứu Google Search thời gian thực (Grounding).</param>
    /// <param name="ConversationHistory">Lịch sử hội thoại các lượt trước (role 'user' và 'model').</param>
    public sealed record GeminiRequestOptions(
        string? SystemInstruction = null,
        object? ResponseSchema = null,
        double? Temperature = null,
        int? ThinkingBudget = null,
        bool EnableGoogleSearch = false,
        IReadOnlyList<GeminiChatMessage>? ConversationHistory = null);

    /// <summary>Kết quả một lần gọi Gemini thành công.</summary>
    public sealed record GeminiResult(string Text, string Model);

    public interface IGeminiClient
    {
        /// <summary>Gọi Gemini, thử lần lượt nhiều model, trả về văn bản JSON cùng tên model đã phản hồi.</summary>
        Task<GeminiResult> GenerateAsync(string prompt, GeminiRequestOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>Tiện ích: chỉ lấy nội dung văn bản (JSON) của phản hồi.</summary>
        async Task<string> GenerateJsonAsync(string prompt, GeminiRequestOptions? options = null, CancellationToken cancellationToken = default) =>
            (await GenerateAsync(prompt, options, cancellationToken)).Text;
    }
}
