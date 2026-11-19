using System.Text;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Explainer
{
    /// <summary>Dựng prompt sinh kịch bản video ngắn (TikTok/Shorts/Reels), tách vai trò khỏi dữ liệu người dùng.</summary>
    public static class ScriptPromptBuilder
    {
        public const string DefaultTone = "Năng động, cuốn hút, giàu thông tin thực tế";
        public const string DefaultDuration = "30-45 giây";

        private const int MinSceneCount = 4;
        private const int MaxSceneCount = 8;
        private const double SecondsPerScene = 6.0;
        private const int MaxStructureLength = 800;
        private const int MaxIssues = 10;

        /// <summary>Số cảnh gợi ý theo thời lượng (khoảng 6 giây/cảnh).</summary>
        public static int SuggestSceneCount(DurationRange range) =>
            Math.Clamp((int)Math.Round(range.MidSeconds / SecondsPerScene), MinSceneCount, MaxSceneCount);

        public static ExplainerPrompt Build(
            GenerateScriptRequest request, ContentDNA? dna, string tone, string targetDuration,
            IReadOnlyList<string>? previousIssues = null)
        {
            var range = NarrationPacing.ParseTarget(targetDuration);
            var sceneCount = SuggestSceneCount(range);
            var totalWords = NarrationPacing.TargetWords(range);

            return new ExplainerPrompt(BuildSystem(), BuildUser(request, dna, tone, targetDuration, range, sceneCount, totalWords, previousIssues));
        }

        private static string BuildSystem()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Bạn là chuyên gia sáng tạo kịch bản video ngắn (TikTok, YouTube Shorts, Reels) hàng đầu.");
            builder.AppendLine("Nhiệm vụ: viết kịch bản video ngắn giữ chân người xem từ giây đầu tiên tới hết video.");
            builder.AppendLine();
            builder.AppendLine("CẤU TRÚC BẮT BUỘC:");
            builder.AppendLine("1. hook (3 giây đầu): đánh vào tâm lý, gây tò mò cực độ, đọc được trong khoảng 3 giây, không mở bằng lời chào.");
            builder.AppendLine("2. scenes: mỗi cảnh một ý rõ ràng, nối liền mạch với cảnh trước, không lặp ý. Cảnh đầu tiếp nối hook.");
            builder.AppendLine("   - narration: lời thuyết minh tiếng Việt tự nhiên để đọc thành tiếng, câu ngắn, không emoji.");
            builder.AppendLine("   - visualDescription: mô tả hình ảnh bằng tiếng Việt.");
            builder.AppendLine("   - visualKeyword: 2-4 từ tiếng Anh cụ thể, hình dung được để tìm stock footage (ví dụ \"person typing laptop\"), tránh từ trừu tượng.");
            builder.AppendLine("   - duration: số giây của cảnh; phải khớp độ dài narration (đọc khoảng 2.8 từ/giây).");
            builder.AppendLine("3. cta: lời kêu gọi hành động ngắn cuối video (theo dõi, lưu video, bình luận).");
            builder.AppendLine();
            builder.AppendLine("QUY TẮC:");
            builder.AppendLine("- Chỉ nêu số liệu, dữ kiện bạn chắc chắn đúng; không bịa số liệu hay nguồn.");
            builder.AppendLine("- Tổng duration các cảnh và tổng số từ lời thoại phải khớp thời lượng mục tiêu.");
            builder.AppendLine("- Nếu có thông tin thương hiệu/giọng điệu thì bám theo, nhưng không để nó làm sai lệch dữ kiện.");
            builder.AppendLine("- sceneNo đánh số liên tục từ 1.");
            builder.AppendLine();
            builder.Append(PromptSanitizer.DataBlockRule);
            return builder.ToString();
        }

        private static string BuildUser(
            GenerateScriptRequest request, ContentDNA? dna, string tone, string targetDuration,
            DurationRange range, int sceneCount, int totalWords, IReadOnlyList<string>? previousIssues)
        {
            var lines = new List<string>
            {
                PromptSanitizer.Wrap("Chủ đề", request.Topic, 300),
                PromptSanitizer.Wrap("Tone giọng", tone, 200),
                $"Thời lượng mục tiêu: {PromptSanitizer.Clean(targetDuration, 50)} ({range.MinSeconds}-{range.MaxSeconds} giây) → khoảng {sceneCount} cảnh, tổng lời thoại khoảng {totalWords} từ."
            };

            if (dna != null)
            {
                lines.Add(PromptSanitizer.Wrap("Thương hiệu", dna.BrandName, 100));
                lines.Add(PromptSanitizer.Wrap("Giọng điệu thương hiệu", dna.VoiceProfile, 300));
                lines.Add(PromptSanitizer.Wrap("Cấu trúc kịch bản ưa thích (Hook/Context/Mechanism/Twist)", dna.ScriptStructure, MaxStructureLength));
            }

            lines.Add(PromptSanitizer.Wrap("Yêu cầu bổ sung", request.CustomPrompt, 500));

            var text = PromptSanitizer.JoinLines(lines);
            if (previousIssues is { Count: > 0 })
            {
                var feedback = new StringBuilder();
                feedback.AppendLine().AppendLine().AppendLine("BẢN TRƯỚC CÓ LỖI, HÃY SỬA TẤT CẢ:");
                foreach (var issue in previousIssues.Take(MaxIssues)) feedback.AppendLine($"- {issue}");
                text += feedback.ToString().TrimEnd();
            }

            return text;
        }
    }
}
