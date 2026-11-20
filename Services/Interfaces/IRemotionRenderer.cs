using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Explainer;

namespace PostFlow.Services.Interfaces
{
    /// <summary>Số frame/giây của composition Remotion (phải khớp với <c>fps</c> trong RemotionEngine/src/Root.tsx).</summary>
    public static class RemotionDefaults
    {
        public const int Fps = 30;
    }

    public sealed record RemotionSubtitle(
        [property: JsonPropertyName("word")] string Word,
        [property: JsonPropertyName("start_ms")] double StartMs,
        [property: JsonPropertyName("end_ms")] double EndMs);

    public sealed record RemotionChapter(string Id, string Title, int StartFrame, int DurationInFrames);

    public sealed record RemotionTheme(string PrimaryColor, string AccentColor, string BackgroundColor)
    {
        public static RemotionTheme Finance { get; } = new("#eab308", "#10b981", "#05130f");
        public static RemotionTheme Tech { get; } = new("#3b82f6", "#8b5cf6", "#07090e");

        public static RemotionTheme ForCategory(string? category) =>
            ExplainerCategories.IsFinance(category) ? Finance : Tech;
    }

    /// <summary>Props truyền vào composition Remotion (tên thuộc tính được serialize theo camelCase).</summary>
    public sealed record RemotionRenderProps(
        string Topic,
        string Category,
        string AspectRatio,
        int DurationInFrames,
        string AudioPath,
        IReadOnlyList<RemotionSubtitle> Subtitles,
        IReadOnlyList<RemotionChapter> Chapters,
        IReadOnlyList<ExplainerSceneDto> Scenes,
        RemotionTheme Theme);

    public interface IRemotionRenderer
    {
        /// <summary>Đưa file audio vào thư mục public của RemotionEngine để <c>staticFile()</c> đọc được. Trả về đường dẫn tương đối.</summary>
        string StageAudio(string jobId, string sourceAudioPath);

        /// <summary>Xóa audio đã đưa vào thư mục public (an toàn khi gọi nhiều lần).</summary>
        void ReleaseAudio(string jobId);

        Task RenderAsync(string aspectRatio, RemotionRenderProps props, string workDirectory, string outputMp4Path, CancellationToken cancellationToken = default);
    }
}
