using System.Text.RegularExpressions;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Explainer
{
    /// <summary>
    /// Kiểm tra chất lượng đầu ra của AI. Trả về danh sách lỗi (tiếng Việt) để đưa lại cho AI sửa;
    /// danh sách rỗng nghĩa là đạt.
    /// </summary>
    public static partial class ExplainerOutputValidator
    {
        private const double HookOverlapThreshold = 0.4;
        private const int MinWordLength = 3;
        private const int MinWordsForDiacriticCheck = 20;
        private const double MinAccentedWordRatio = 0.25;

        [GeneratedRegex(@"\d+")]
        private static partial Regex DigitsRegex();

        [GeneratedRegex(@"[\p{L}\p{N}]+")]
        private static partial Regex WordRegex();

        // ───────────────────────── Research ─────────────────────────

        public static IReadOnlyList<string> ValidateResearch(ExplainerResearchResponse result)
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(result.CoreDefinition))
                issues.Add("Thiếu coreDefinition.");
            if (result.KeyFacts.Count(fact => !string.IsNullOrWhiteSpace(fact)) < 3)
                issues.Add("keyFacts phải có ít nhất 3 dữ kiện cụ thể.");
            if (result.RecommendedHooks.Count(hook => !string.IsNullOrWhiteSpace(hook)) < 1)
                issues.Add("recommendedHooks phải có ít nhất 1 câu mở đầu.");
            if (result.SuggestedChapters.Count(chapter => !string.IsNullOrWhiteSpace(chapter.Title)) < ExplainerPromptBuilder.MinScenes)
                issues.Add($"suggestedChapters phải có ít nhất {ExplainerPromptBuilder.MinScenes} chương, mỗi chương có title.");

            return issues;
        }

        // ───────────────────────── Storyboard ─────────────────────────

        public static IReadOnlyList<string> ValidateStoryboard(
            ExplainerStoryboardResponse result, ExplainerRenderRequest request)
        {
            var issues = new List<string>();
            var scenes = result.Scenes ?? new List<ExplainerSceneDto>();

            if (scenes.Count < ExplainerPromptBuilder.MinScenes)
            {
                issues.Add($"Chỉ có {scenes.Count} cảnh, cần ít nhất {ExplainerPromptBuilder.MinScenes} cảnh.");
            }

            var plan = ExplainerPromptBuilder.PlanScenes(request);
            if (scenes.Count > 0 && scenes.Count != plan.Count)
            {
                issues.Add($"Cần đúng {plan.Count} cảnh theo kế hoạch, hiện có {scenes.Count}.");
            }

            var allowsCode = string.Equals(request.Category, ExplainerCategories.Tech, StringComparison.OrdinalIgnoreCase)
                             || string.IsNullOrWhiteSpace(request.Category);

            for (var i = 0; i < scenes.Count; i++)
            {
                ValidateScene(scenes[i], i + 1, i < plan.Count ? plan[i] : null, allowsCode, issues);
            }

            ValidateDiacritics(scenes.Select(scene => scene.Narration), issues);
            ValidateWordBudget(scenes, request, issues);
            ValidateFactGrounding(scenes, request.ResearchDossier, issues);
            ValidateHook(scenes, request.SelectedHook, issues);

            return issues;
        }

        private static void ValidateScene(
            ExplainerSceneDto scene, int number, ScenePlan? planned, bool allowsCode, List<string> issues)
        {
            var label = $"Cảnh {number}";
            var type = ExplainerSceneNormalizer.NormalizeSceneType(scene.Type);

            if (string.IsNullOrWhiteSpace(scene.Narration))
                issues.Add($"{label}: narration đang trống.");
            if (string.IsNullOrWhiteSpace(scene.Title))
                issues.Add($"{label}: thiếu title.");

            if (planned != null && !string.Equals(type, planned.Type, StringComparison.OrdinalIgnoreCase))
                issues.Add($"{label}: type phải là \"{planned.Type}\" theo kế hoạch, hiện là \"{scene.Type}\".");

            if (type == ExplainerSceneTypes.CodeSnippet && !allowsCode)
                issues.Add($"{label}: không được dùng code-snippet cho lĩnh vực này.");

            switch (type)
            {
                case ExplainerSceneTypes.Flowchart when (scene.FlowchartNodes?.Count ?? 0) < 3:
                    issues.Add($"{label}: flowchart cần ít nhất 3 node trong flowchartNodes.");
                    break;

                case ExplainerSceneTypes.CardComparison:
                    var cards = scene.ComparisonCards ?? new List<CardComparisonItemDto>();
                    if (cards.Count < 2)
                        issues.Add($"{label}: card-comparison cần đúng 2 thẻ trong comparisonCards.");
                    else if (cards.Count(card => card.IsWinner) != 1)
                        issues.Add($"{label}: card-comparison phải có đúng một thẻ isWinner = true.");
                    break;

                case ExplainerSceneTypes.MetricHighlight when string.IsNullOrWhiteSpace(scene.Metric?.Value):
                    issues.Add($"{label}: metric-highlight thiếu metric.value.");
                    break;

                case ExplainerSceneTypes.CodeSnippet when string.IsNullOrWhiteSpace(scene.CodeSnippet?.Code):
                    issues.Add($"{label}: code-snippet thiếu codeSnippet.code.");
                    break;
            }
        }

        /// <summary>Lời thoại tiếng Việt phải có dấu: bản không dấu sẽ bị TTS đọc sai.</summary>
        internal static void ValidateDiacritics(IEnumerable<string?> narrations, List<string> issues)
        {
            var words = narrations
                .SelectMany(text => WordRegex().Matches(text ?? string.Empty).Select(match => match.Value))
                .ToList();
            if (words.Count < MinWordsForDiacriticCheck) return;

            var accented = words.Count(word => word.Any(ch => ch > 'ÿ' || ch is 'đ' or 'Đ' || "àáâãèéêìíòóôõùúýÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝ".Contains(ch)));
            if (accented / (double)words.Count < MinAccentedWordRatio)
            {
                issues.Add("narration đang viết tiếng Việt không dấu; hãy viết tiếng Việt có dấu đầy đủ.");
            }
        }

        private static void ValidateWordBudget(
            IReadOnlyList<ExplainerSceneDto> scenes, ExplainerRenderRequest request, List<string> issues)
        {
            if (scenes.Count == 0) return;

            var range = NarrationPacing.ParseTarget(request.TargetDuration);
            var (min, max) = NarrationPacing.WordBudget(range);
            var words = scenes.Sum(scene => NarrationSanitizer.CountWords(NarrationSanitizer.Clean(scene.Narration)));

            if (words < min)
                issues.Add($"Tổng lời thoại chỉ {words} từ, cần khoảng {NarrationPacing.TargetWords(range)} từ (tối thiểu {min}) cho thời lượng {request.TargetDuration}.");
            else if (words > max)
                issues.Add($"Tổng lời thoại {words} từ, quá dài so với thời lượng {request.TargetDuration}; cần khoảng {NarrationPacing.TargetWords(range)} từ (tối đa {max}).");
        }

        /// <summary>Số liệu ở cảnh metric phải có trong dữ liệu nghiên cứu, tránh AI tự bịa con số.</summary>
        private static void ValidateFactGrounding(
            IReadOnlyList<ExplainerSceneDto> scenes, ExplainerResearchDossierDto? dossier, List<string> issues)
        {
            if (dossier == null) return;

            var source = string.Join(" ", new[] { dossier.CoreDefinition }
                .Concat(dossier.KeyFacts ?? new List<string>()));
            var sourceNumbers = DigitsRegex().Matches(source).Select(match => match.Value).ToHashSet();
            if (sourceNumbers.Count == 0) return; // Dữ liệu không có con số nào để đối chiếu.

            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                if (ExplainerSceneNormalizer.NormalizeSceneType(scene.Type) != ExplainerSceneTypes.MetricHighlight) continue;

                var metricNumbers = DigitsRegex().Matches(scene.Metric?.Value ?? string.Empty)
                    .Select(match => match.Value)
                    .ToList();
                if (metricNumbers.Count > 0 && !metricNumbers.Any(sourceNumbers.Contains))
                {
                    issues.Add($"Cảnh {i + 1}: số liệu \"{scene.Metric!.Value}\" không có trong dữ liệu nghiên cứu; chỉ dùng con số từ danh sách dữ kiện.");
                }
            }
        }

        /// <summary>Cảnh đầu phải bám ý của hook đã chọn (đối chiếu theo tỷ lệ từ khoá trùng).</summary>
        private static void ValidateHook(IReadOnlyList<ExplainerSceneDto> scenes, string? hook, List<string> issues)
        {
            if (scenes.Count == 0 || string.IsNullOrWhiteSpace(hook)) return;

            var hookWords = SignificantWords(hook);
            if (hookWords.Count == 0) return;

            var first = scenes[0];
            var firstWords = SignificantWords($"{first.Narration} {first.Title} {first.Subtitle}");
            var overlap = hookWords.Count(firstWords.Contains) / (double)hookWords.Count;

            if (overlap < HookOverlapThreshold)
            {
                issues.Add("Cảnh 1: narration chưa bám theo hook đã chọn; hãy mở đầu bằng ý của hook.");
            }
        }

        private static HashSet<string> SignificantWords(string? text) =>
            WordRegex().Matches((text ?? string.Empty).ToLowerInvariant())
                .Select(match => match.Value)
                .Where(word => word.Length >= MinWordLength || word.Any(char.IsDigit))
                .ToHashSet();

        // ───────────────────────── Script (video ngắn) ─────────────────────────

        public static IReadOnlyList<string> ValidateScript(ScriptDto script, string? targetDuration)
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(script.Title)) issues.Add("Thiếu title.");
            if (string.IsNullOrWhiteSpace(script.Hook)) issues.Add("Thiếu hook mở đầu.");
            if (string.IsNullOrWhiteSpace(script.Cta)) issues.Add("Thiếu cta kêu gọi hành động.");
            if (script.Scenes.Count < ExplainerPromptBuilder.MinScenes)
                issues.Add($"Chỉ có {script.Scenes.Count} cảnh, cần ít nhất {ExplainerPromptBuilder.MinScenes} cảnh.");

            foreach (var scene in script.Scenes)
            {
                var label = $"Cảnh {scene.SceneNo}";
                if (string.IsNullOrWhiteSpace(scene.Narration)) issues.Add($"{label}: narration đang trống.");
                if (string.IsNullOrWhiteSpace(scene.VisualKeyword)) issues.Add($"{label}: thiếu visualKeyword.");
                if (scene.Duration <= 0) issues.Add($"{label}: duration phải lớn hơn 0.");
            }

            if (script.Scenes.Count > 0)
            {
                ValidateDiacritics(script.Scenes.Select(scene => scene.Narration), issues);
                var range = NarrationPacing.ParseTarget(targetDuration);
                var total = script.Scenes.Sum(scene => scene.Duration);
                var lower = (int)Math.Floor(range.MinSeconds * 0.7);
                var upper = (int)Math.Ceiling(range.MaxSeconds * 1.3);
                if (total < lower || total > upper)
                {
                    issues.Add($"Tổng duration các cảnh là {total} giây, cần nằm trong khoảng {lower}-{upper} giây (mục tiêu {targetDuration}).");
                }

                var (minWords, maxWords) = NarrationPacing.WordBudget(range);
                var words = script.Scenes.Sum(scene => NarrationSanitizer.CountWords(scene.Narration))
                            + NarrationSanitizer.CountWords(script.Hook)
                            + NarrationSanitizer.CountWords(script.Cta);
                if (words > maxWords)
                    issues.Add($"Lời thoại {words} từ quá dài so với thời lượng, cần tối đa khoảng {maxWords} từ.");
                else if (words < minWords)
                    issues.Add($"Lời thoại chỉ {words} từ, cần tối thiểu khoảng {minWords} từ.");
            }

            return issues;
        }
    }
}
