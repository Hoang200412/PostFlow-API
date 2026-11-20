using System;
using System.Collections.Generic;
using System.Linq;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Explainer;

namespace PostFlow.Services.Script
{
    public static class ScriptOutputValidator
    {
        public static IReadOnlyList<string> ValidateScript(ScriptDto script, string? targetDuration)
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(script.Title)) issues.Add("Thiếu title.");
            if (string.IsNullOrWhiteSpace(script.Hook)) issues.Add("Thiếu hook mở đầu.");
            if (string.IsNullOrWhiteSpace(script.Cta)) issues.Add("Thiếu cta kêu gọi hành động.");
            if (script.Scenes.Count < 3)
                issues.Add($"Chỉ có {script.Scenes.Count} cảnh, cần ít nhất 3 cảnh.");

            foreach (var scene in script.Scenes)
            {
                var label = $"Cảnh {scene.SceneNo}";
                if (string.IsNullOrWhiteSpace(scene.Narration)) issues.Add($"{label}: narration đang trống.");
                if (string.IsNullOrWhiteSpace(scene.VisualKeyword)) issues.Add($"{label}: thiếu visualKeyword.");
                if (scene.Duration <= 0) issues.Add($"{label}: duration phải lớn hơn 0.");
            }

            if (script.Scenes.Count > 0)
            {
                var range = NarrationHelper.ParseTarget(targetDuration);
                var total = script.Scenes.Sum(scene => scene.Duration);
                var lower = (int)Math.Floor(range.MinSeconds * 0.7);
                var upper = (int)Math.Ceiling(range.MaxSeconds * 1.3);
                if (total < lower || total > upper)
                {
                    issues.Add($"Tổng duration các cảnh là {total} giây, cần nằm trong khoảng {lower}-{upper} giây (mục tiêu {targetDuration}).");
                }

                var (minWords, maxWords) = NarrationHelper.WordBudget(range);
                var words = script.Scenes.Sum(scene => NarrationHelper.CountWords(scene.Narration))
                            + NarrationHelper.CountWords(script.Hook)
                            + NarrationHelper.CountWords(script.Cta);
                if (words > maxWords)
                    issues.Add($"Lời thoại {words} từ quá dài so với thời lượng, cần tối đa khoảng {maxWords} từ.");
                else if (words < minWords)
                    issues.Add($"Lời thoại chỉ {words} từ, cần tối thiểu khoảng {minWords} từ.");
            }

            return issues;
        }
    }
}
