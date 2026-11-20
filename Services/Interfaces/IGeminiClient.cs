using System.Threading;
using System.Threading.Tasks;

namespace PostFlow.Services.Interfaces
{
    /// <summary>Tuỳ chọn cho một lần gọi Gemini: vai trò/quy tắc (system), schema đầu ra và nhiệt độ sinh.</summary>
    /// <param name="SystemInstruction">Vai trò và quy tắc cố định, tách khỏi dữ liệu người dùng.</param>
    /// <param name="ResponseSchema">Schema JSON (dạng OpenAPI subset của Gemini) ép cấu trúc đầu ra.</param>
    /// <param name="Temperature">Ghi đè nhiệt độ mặc định.</param>
    public sealed record GeminiRequestOptions(
        string? SystemInstruction = null,
        object? ResponseSchema = null,
        double? Temperature = null);

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
