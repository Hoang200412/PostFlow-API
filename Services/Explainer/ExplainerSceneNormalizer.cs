using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Explainer
{
    public static class ExplainerSceneTypes
    {
        public const string Hero = "hero";
        public const string Flowchart = "flowchart";
        public const string CardComparison = "card-comparison";
        public const string MetricHighlight = "metric-highlight";
        public const string CodeSnippet = "code-snippet";
        public const string KeyTakeaway = "key-takeaway";
        public const string Timeline = "timeline";
        public const string BarChart = "bar-chart";
        public const string Quote = "quote";

        public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Hero, Flowchart, CardComparison, MetricHighlight, CodeSnippet, KeyTakeaway, Timeline, BarChart, Quote
        };
    }

    /// <summary>Chuẩn hóa loại scene và suy ra icon mặc định cho scene do AI hoặc người dùng cung cấp.</summary>
    public static class ExplainerSceneNormalizer
    {
        // Thứ tự quan trọng: quy tắc đứng trước thắng. Loại cụ thể phải đứng trước quy tắc chung ("chart" → flowchart).
        private static readonly (string SceneType, string[] Keywords)[] SceneTypeRules =
        {
            (ExplainerSceneTypes.BarChart, new[] { "barchart", "bargraph", "columnchart" }),
            (ExplainerSceneTypes.Timeline, new[] { "timeline", "chronology", "milestone" }),
            (ExplainerSceneTypes.Quote, new[] { "quote", "testimonial" }),
            (ExplainerSceneTypes.KeyTakeaway, new[] { "takeaway", "summary", "conclusion" }),
            (ExplainerSceneTypes.Flowchart, new[] { "flow", "chart", "arch", "diagram", "process", "pipeline", "workflow" }),
            (ExplainerSceneTypes.CardComparison, new[] { "compar", "card", "versus", "table" }),
            (ExplainerSceneTypes.MetricHighlight, new[] { "metric", "stat", "number", "highlight", "benchmark" }),
            (ExplainerSceneTypes.CodeSnippet, new[] { "code", "snippet", "terminal", "syntax", "script" }),
        };

        private static readonly (string Icon, string[] Keywords)[] FinanceIconRules =
        {
            ("dollar", new[] { "tiền", "thu nhập", "đô", "usd", "giàu" }),
            ("trending-up", new[] { "đầu tư", "tăng trưởng", "lợi nhuận", "lãi" }),
            ("wallet", new[] { "tiết kiệm", "chi phí", "ví", "ngân sách" }),
            ("barchart", new[] { "lạm phát", "thị trường", "thống kê", "chu kỳ" }),
            ("shield", new[] { "rủi ro", "bảo vệ", "an toàn", "bảo hiểm" }),
            ("target", new[] { "mục tiêu", "chiến lược" }),
            ("landmark", new[] { "ngân hàng", "thể chế" }),
        };

        private static readonly (string Icon, string[] Keywords)[] TechIconRules =
        {
            ("database", new[] { "database", "db", "lưu trữ", "sql" }),
            ("shield", new[] { "bảo mật", "security", "gateway", "shield", "auth" }),
            ("server", new[] { "server", "máy chủ", "backend" }),
            ("boxes", new[] { "cloud", "đám mây", "docker", "k8s", "container" }),
            ("zap", new[] { "tốc độ", "hiệu năng", "fast", "speed" }),
            ("network", new[] { "mạng", "network", "kết nối" }),
            ("terminal", new[] { "code", "lập trình", "terminal" }),
        };

        public static string NormalizeSceneType(string? rawType)
        {
            var lower = (rawType ?? string.Empty).Trim().ToLowerInvariant();
            if (ExplainerSceneTypes.Known.Contains(lower)) return lower;

            var compact = lower.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
            if (compact == "vs") return ExplainerSceneTypes.CardComparison;

            foreach (var (sceneType, keywords) in SceneTypeRules)
            {
                if (ContainsAny(compact, keywords)) return sceneType;
            }

            return ExplainerSceneTypes.Hero;
        }

        public static string DeduceVisualIcon(string sceneType, string? title, string? category)
        {
            var text = (title ?? string.Empty).ToLowerInvariant();

            if (ExplainerCategories.IsFinance(category))
            {
                return FindIcon(FinanceIconRules, text) ?? sceneType switch
                {
                    ExplainerSceneTypes.Flowchart => "trending-up",
                    ExplainerSceneTypes.CardComparison => "piechart",
                    ExplainerSceneTypes.MetricHighlight => "coins",
                    _ => "dollar"
                };
            }

            return FindIcon(TechIconRules, text) ?? sceneType switch
            {
                ExplainerSceneTypes.Flowchart => "network",
                ExplainerSceneTypes.CardComparison => "layers",
                ExplainerSceneTypes.MetricHighlight => "zap",
                ExplainerSceneTypes.CodeSnippet => "terminal",
                _ => "sparkles"
            };
        }

        /// <summary>
        /// Cảnh thiếu dữ liệu bắt buộc của loại cảnh (sơ đồ không đủ node, so sánh không đủ 2 thẻ, số liệu rỗng, code rỗng)
        /// sẽ render thành khung trống. Hạ những cảnh đó về "hero" (chỉ cần title/subtitle) và trả về số cảnh đã hạ.
        /// </summary>
        public static int DowngradeIncompleteScenes(IEnumerable<ExplainerSceneDto> scenes)
        {
            var downgraded = 0;
            foreach (var scene in scenes)
            {
                if (!IsMissingRequiredData(scene)) continue;
                scene.Type = ExplainerSceneTypes.Hero;
                downgraded++;
            }
            return downgraded;
        }

        private static bool IsMissingRequiredData(ExplainerSceneDto scene) => scene.Type switch
        {
            ExplainerSceneTypes.Flowchart => (scene.FlowchartNodes?.Count ?? 0) < 2,
            ExplainerSceneTypes.CardComparison => (scene.ComparisonCards?.Count ?? 0) < 2,
            ExplainerSceneTypes.MetricHighlight => string.IsNullOrWhiteSpace(scene.Metric?.Value),
            ExplainerSceneTypes.CodeSnippet => string.IsNullOrWhiteSpace(scene.CodeSnippet?.Code),
            _ => false
        };

        /// <summary>Icon mặc định khi scene chưa có icon.</summary>
        public static string DefaultIcon(string? category) =>
            ExplainerCategories.IsFinance(category) ? "dollar" : "sparkles";

        /// <summary>Đảm bảo scene có Type chuẩn hóa, Category và VisualIcon hợp lệ.</summary>
        public static void ApplyDefaults(IEnumerable<ExplainerSceneDto> scenes, string? category)
        {
            var resolvedCategory = category ?? ExplainerCategories.Tech;
            foreach (var scene in scenes)
            {
                scene.Type = (scene.Type ?? ExplainerSceneTypes.Hero).Trim().ToLowerInvariant();
                scene.Category = resolvedCategory;
                if (string.IsNullOrWhiteSpace(scene.VisualIcon))
                {
                    scene.VisualIcon = DefaultIcon(resolvedCategory);
                }
            }
        }

        private static string? FindIcon((string Icon, string[] Keywords)[] rules, string text)
        {
            foreach (var (icon, keywords) in rules)
            {
                if (ContainsAny(text, keywords)) return icon;
            }
            return null;
        }

        private static bool ContainsAny(string text, string[] keywords) =>
            keywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal));
    }
}
