namespace PostFlow.Services.Explainer
{
    /// <summary>Schema đầu ra (responseSchema của Gemini) cho các lần sinh nội dung.</summary>
    internal static class GeminiSchemas
    {
        private static Dictionary<string, object> Str(string? description = null, bool nullable = false)
        {
            var schema = new Dictionary<string, object> { ["type"] = "STRING" };
            if (description != null) schema["description"] = description;
            if (nullable) schema["nullable"] = true;
            return schema;
        }

        private static Dictionary<string, object> Int(string? description = null) =>
            description == null
                ? new Dictionary<string, object> { ["type"] = "INTEGER" }
                : new Dictionary<string, object> { ["type"] = "INTEGER", ["description"] = description };

        private static Dictionary<string, object> Bool() => new() { ["type"] = "BOOLEAN" };

        private static Dictionary<string, object> StrEnum(params string[] values) =>
            new() { ["type"] = "STRING", ["format"] = "enum", ["enum"] = values };

        private static Dictionary<string, object> ArrayOf(object items, bool nullable = false)
        {
            var schema = new Dictionary<string, object> { ["type"] = "ARRAY", ["items"] = items };
            if (nullable) schema["nullable"] = true;
            return schema;
        }

        private static Dictionary<string, object> Obj(
            Dictionary<string, object> properties, string[] required, bool nullable = false)
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = "OBJECT",
                ["properties"] = properties,
                ["required"] = required
            };
            if (nullable) schema["nullable"] = true;
            return schema;
        }

        /// <summary>Schema kịch bản phân cảnh Explainer (storyboard).</summary>
        public static object Storyboard()
        {
            var flowchartNode = Obj(new()
            {
                ["id"] = Str(),
                ["label"] = Str(),
                ["sublabel"] = Str(nullable: true),
                ["icon"] = Str(nullable: true),
                ["status"] = StrEnum("normal", "active", "highlight")
            }, new[] { "id", "label" });

            var comparisonCard = Obj(new()
            {
                ["name"] = Str(),
                ["badge"] = Str(nullable: true),
                ["icon"] = Str(nullable: true),
                ["pros"] = ArrayOf(Str()),
                ["cons"] = ArrayOf(Str(), nullable: true),
                ["isWinner"] = Bool()
            }, new[] { "name", "pros", "isWinner" });

            var metric = Obj(new()
            {
                ["value"] = Str("Con số hiển thị, ví dụ 72 hoặc 3.5"),
                ["prefix"] = Str(nullable: true),
                ["suffix"] = Str(nullable: true),
                ["label"] = Str(),
                ["description"] = Str(nullable: true)
            }, new[] { "value", "label" }, nullable: true);

            var codeSnippet = Obj(new()
            {
                ["language"] = Str(),
                ["code"] = Str()
            }, new[] { "language", "code" }, nullable: true);

            var scene = Obj(new()
            {
                ["id"] = Str(),
                ["type"] = StrEnum(
                    ExplainerSceneTypes.Hero,
                    ExplainerSceneTypes.Flowchart,
                    ExplainerSceneTypes.CardComparison,
                    ExplainerSceneTypes.MetricHighlight,
                    ExplainerSceneTypes.CodeSnippet),
                ["visualIcon"] = Str(),
                ["tags"] = ArrayOf(Str(), nullable: true),
                ["title"] = Str(),
                ["subtitle"] = Str(nullable: true),
                ["narration"] = Str("Lời thoại tiếng Việt, đọc thành tiếng, không ký hiệu đặc biệt"),
                ["durationInFrames"] = Int("Số khung hình ở 30fps (ước lượng)"),
                ["flowchartNodes"] = ArrayOf(flowchartNode, nullable: true),
                ["comparisonCards"] = ArrayOf(comparisonCard, nullable: true),
                ["metric"] = metric,
                ["codeSnippet"] = codeSnippet
            }, new[] { "id", "type", "title", "narration", "durationInFrames" });

            return Obj(new()
            {
                ["title"] = Str(),
                ["totalEstimatedSeconds"] = Int(),
                ["scenes"] = ArrayOf(scene)
            }, new[] { "title", "totalEstimatedSeconds", "scenes" });
        }

        /// <summary>Schema kết quả nghiên cứu chủ đề.</summary>
        public static object Research()
        {
            var chapter = Obj(new()
            {
                ["title"] = Str(),
                ["description"] = Str(),
                ["layoutType"] = StrEnum(
                    ExplainerSceneTypes.Hero,
                    ExplainerSceneTypes.Flowchart,
                    ExplainerSceneTypes.CardComparison,
                    ExplainerSceneTypes.MetricHighlight,
                    ExplainerSceneTypes.CodeSnippet)
            }, new[] { "title", "description", "layoutType" });

            return Obj(new()
            {
                ["coreDefinition"] = Str(),
                ["keyFacts"] = ArrayOf(Str()),
                ["commonMisconceptions"] = ArrayOf(Str()),
                ["recommendedHooks"] = ArrayOf(Str()),
                ["suggestedChapters"] = ArrayOf(chapter)
            }, new[] { "coreDefinition", "keyFacts", "commonMisconceptions", "recommendedHooks", "suggestedChapters" });
        }

        /// <summary>Schema kịch bản video ngắn (hook, các cảnh, CTA).</summary>
        public static object Script()
        {
            var scene = Obj(new()
            {
                ["sceneNo"] = Int(),
                ["narration"] = Str("Lời thuyết minh tiếng Việt của cảnh"),
                ["visualDescription"] = Str("Mô tả hình ảnh trực quan bằng tiếng Việt"),
                ["visualKeyword"] = Str("Từ khóa tiếng Anh 2-4 từ để tìm stock footage"),
                ["duration"] = Int("Thời lượng cảnh tính bằng giây")
            }, new[] { "sceneNo", "narration", "visualDescription", "visualKeyword", "duration" });

            return Obj(new()
            {
                ["title"] = Str(),
                ["topic"] = Str(),
                ["hook"] = Str(),
                ["scenes"] = ArrayOf(scene),
                ["cta"] = Str()
            }, new[] { "title", "topic", "hook", "scenes", "cta" });
        }
    }
}
