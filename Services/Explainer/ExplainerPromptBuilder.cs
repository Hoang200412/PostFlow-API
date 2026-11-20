using System.Text;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Explainer
{
    /// <summary>Một yêu cầu gửi AI, tách vai trò/quy tắc cố định (System) khỏi dữ liệu của lần gọi (User).</summary>
    public sealed record ExplainerPrompt(string System, string User);

    /// <summary>Một phân cảnh trong kế hoạch gửi cho AI.</summary>
    public sealed record ScenePlan(int Index, string Type, string? ChapterTitle, string? ChapterDescription);

    /// <summary>Dựng prompt gửi AI cho bước nghiên cứu chủ đề và thiết kế storyboard.</summary>
    public static class ExplainerPromptBuilder
    {
        public const int MinScenes = 3;
        public const int MaxScenes = 6;

        private const string DefaultStoryboardDuration = "30-45 giây";
        private const int MaxFactLength = 400;
        private const int MaxListItems = 10;

        private static readonly string[] DefaultPlanTypes =
        {
            ExplainerSceneTypes.Hero,
            ExplainerSceneTypes.Flowchart,
            ExplainerSceneTypes.CardComparison,
            ExplainerSceneTypes.MetricHighlight
        };

        // Chỉ các loại cảnh Remotion render được ở bước này.
        private static readonly HashSet<string> RenderableTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ExplainerSceneTypes.Hero,
            ExplainerSceneTypes.Flowchart,
            ExplainerSceneTypes.CardComparison,
            ExplainerSceneTypes.MetricHighlight,
            ExplainerSceneTypes.CodeSnippet
        };

        // ───────────────────────── Research ─────────────────────────

        public static ExplainerPrompt BuildResearchPrompt(
            ExplainerResearchRequest request, IReadOnlyList<string>? previousIssues = null)
        {
            var profile = CategoryProfile.For(request.Category);

            var system = PromptSanitizer.JoinLines(new[]
            {
                profile.ResearchRole,
                "Nhiệm vụ: nghiên cứu chuyên sâu một chủ đề để làm nền cho video giải thích ngắn.",
                "QUY TẮC:",
                "- Chỉ nêu dữ kiện, số liệu bạn tin chắc là đúng và kiểm chứng được. Không bịa số liệu, nguồn hay tên người.",
                "- Nếu không chắc về con số cụ thể, dùng mô tả định tính hoặc thứ tự độ lớn thay vì số chính xác.",
                "- keyFacts: 4-6 dữ kiện cụ thể, mỗi dữ kiện một câu độc lập, ưu tiên có con số hoặc ví dụ thực tế.",
                "- commonMisconceptions: 2-3 hiểu lầm phổ biến kèm cách hiểu đúng ngắn gọn.",
                "- recommendedHooks: 3 câu mở đầu khác kiểu nhau (gợi mở bất ngờ, câu hỏi khiêu khích, tuyên bố gây sốc bằng dữ liệu), mỗi câu đọc được trong khoảng 3 giây.",
                "- suggestedChapters: 4-5 chương theo mạch kể: mở đầu → cơ chế → so sánh → số liệu/kết luận. layoutType chỉ thuộc: hero, flowchart, card-comparison, metric-highlight" + (profile.AllowsCode ? ", code-snippet." : "."),
                "- Viết toàn bộ bằng tiếng Việt tự nhiên, súc tích.",
                PromptSanitizer.DataBlockRule
            });

            var user = PromptSanitizer.JoinLines(new[]
            {
                PromptSanitizer.Wrap("Chủ đề", request.Topic, 300),
                PromptSanitizer.Wrap("Đối tượng khán giả", string.IsNullOrWhiteSpace(request.TargetAudience) ? profile.DefaultAudience : request.TargetAudience, 200),
                PromptSanitizer.Wrap("Văn phong", string.IsNullOrWhiteSpace(request.Tone) ? profile.DefaultTone : request.Tone, 200)
            });

            return new ExplainerPrompt(system, user + FormatIssues(previousIssues));
        }

        private static string FormatIssues(IReadOnlyList<string>? issues)
        {
            if (issues is not { Count: > 0 }) return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine().AppendLine().AppendLine("BẢN TRƯỚC CÓ LỖI, HÃY SỬA TẤT CẢ:");
            foreach (var issue in issues.Take(MaxListItems))
            {
                builder.AppendLine($"- {issue}");
            }
            return builder.ToString().TrimEnd();
        }

        // ───────────────────────── Storyboard ─────────────────────────

        /// <summary>Dựng prompt storyboard. <paramref name="previousIssues"/> là lỗi của bản trước (nếu đang sinh lại).</summary>
        public static ExplainerPrompt BuildStoryboardPrompt(
            ExplainerRenderRequest request, IReadOnlyList<string>? previousIssues = null)
        {
            var profile = CategoryProfile.For(request.Category);
            var plan = PlanScenes(request, profile);
            var range = NarrationHelper.ParseTarget(request.TargetDuration ?? DefaultStoryboardDuration);

            return new ExplainerPrompt(
                BuildStoryboardSystem(profile),
                BuildStoryboardUser(request, profile, plan, range, previousIssues));
        }

        /// <summary>Lập kế hoạch số cảnh và loại cảnh: bám dàn ý chương của bước nghiên cứu, nếu thiếu thì dùng kế hoạch mặc định.</summary>
        public static IReadOnlyList<ScenePlan> PlanScenes(ExplainerRenderRequest request) =>
            PlanScenes(request, CategoryProfile.For(request.Category));

        private static IReadOnlyList<ScenePlan> PlanScenes(ExplainerRenderRequest request, CategoryProfile profile)
        {
            var chapters = (request.ResearchDossier?.Chapters ?? new List<ExplainerChapterDto>())
                .Where(chapter => !string.IsNullOrWhiteSpace(chapter.Title))
                .Take(MaxScenes)
                .ToList();

            var plan = new List<(string Type, string? Title, string? Description)>();
            foreach (var chapter in chapters)
            {
                plan.Add((ResolveRenderableType(chapter.LayoutType, profile), chapter.Title, chapter.Description));
            }

            // Thiếu cảnh so với mức tối thiểu: bổ sung các loại cảnh mặc định chưa dùng.
            foreach (var type in DefaultPlanTypes)
            {
                if (plan.Count >= MinScenes) break;
                if (plan.All(item => item.Type != type)) plan.Add((type, null, null));
            }

            // Không có dàn ý nào: dùng đủ kế hoạch mặc định.
            if (chapters.Count == 0)
            {
                plan.Clear();
                plan.AddRange(DefaultPlanTypes.Select(type => (type, (string?)null, (string?)null)));
            }

            return plan
                .Select((item, index) => new ScenePlan(index + 1, item.Type, item.Title, item.Description))
                .ToList();
        }

        private static string ResolveRenderableType(string? rawType, CategoryProfile profile)
        {
            var type = ExplainerSceneNormalizer.NormalizeSceneType(rawType);

            if (!RenderableTypes.Contains(type))
            {
                type = type switch
                {
                    ExplainerSceneTypes.Timeline or ExplainerSceneTypes.BarChart => ExplainerSceneTypes.Flowchart,
                    _ => ExplainerSceneTypes.Hero
                };
            }

            if (type == ExplainerSceneTypes.CodeSnippet && !profile.AllowsCode)
            {
                type = ExplainerSceneTypes.Flowchart;
            }

            return type;
        }

        private static string BuildStoryboardSystem(CategoryProfile profile)
        {
            var builder = new StringBuilder();
            builder.AppendLine(profile.DirectorRole);
            builder.AppendLine("Nhiệm vụ: thiết kế kịch bản phân cảnh phong cách Motion Graphics hiện đại, mạch lạc từ mở đầu tới kết luận.");
            builder.AppendLine();
            builder.AppendLine("QUY TẮC NỘI DUNG:");
            builder.AppendLine("- Khi có phần \"Dữ liệu nghiên cứu\", đó là nguồn sự thật DUY NHẤT: mọi con số, tên riêng, khẳng định phải lấy từ đó. Không tự thêm số liệu ngoài danh sách.");
            builder.AppendLine("- Khi không có dữ liệu nghiên cứu, chỉ dùng số liệu bạn chắc chắn đúng; nếu không chắc thì mô tả định tính.");
            builder.AppendLine("- Cảnh đầu tiên mở bằng đúng ý của hook đã cho (có thể diễn đạt lại cho mượt khi đọc thành tiếng).");
            builder.AppendLine("- Mỗi cảnh một ý chính; narration các cảnh nối liền mạch, cảnh sau tiếp nối cảnh trước, không lặp ý.");
            builder.AppendLine("- Nếu có hiểu lầm cần đính chính, một cảnh phải nêu rõ hiểu lầm đó và cách hiểu đúng.");
            builder.AppendLine("- Cảnh cuối chốt thông điệp cần nhớ và có lời kêu gọi hành động ngắn (theo dõi, lưu video...).");
            builder.AppendLine("- Mọi văn bản (title, subtitle, narration, nhãn) viết tiếng Việt CÓ DẤU đầy đủ; tuyệt đối không viết không dấu.");
            builder.AppendLine("- narration là tiếng Việt tự nhiên để đọc thành tiếng: câu ngắn, không emoji, không ký hiệu đặc biệt, không viết tắt khó đọc.");
            builder.AppendLine("- Số lượng từ narration phải khớp tổng số từ mục tiêu được nêu ở phần yêu cầu.");
            builder.AppendLine("- Tuyệt đối không để khung hình trống hay chỉ có chữ thô: mỗi cảnh có visualIcon hợp lệ, tags gồm 2 từ khóa nổi bật của chủ đề, và cấu trúc dữ liệu đúng loại cảnh.");
            if (!profile.AllowsCode)
            {
                builder.AppendLine("- Lĩnh vực này KHÔNG dùng loại cảnh \"code-snippet\". Dùng \"flowchart\", \"card-comparison\", \"metric-highlight\" hoặc \"hero\".");
            }
            builder.AppendLine();
            builder.AppendLine($"Danh sách visualIcon hợp lệ: {profile.AllowedIcons}.");
            builder.AppendLine();
            builder.AppendLine("CÁC LOẠI CẢNH (type viết thường):");
            builder.AppendLine("1. \"hero\": mở đầu hoặc kết luận, biểu tượng trung tâm lớn, title, subtitle và tags.");
            builder.AppendLine("2. \"flowchart\": sơ đồ luồng/chu trình 3-4 node (flowchartNodes), mỗi node có label, sublabel, icon.");
            builder.AppendLine("3. \"card-comparison\": so sánh 2 bên (comparisonCards) có badge, icon, pros, cons; đúng một bên isWinner = true.");
            builder.AppendLine("4. \"metric-highlight\": một con số đột phá (metric.value, label, description) kèm đồng hồ đo.");
            builder.AppendLine("5. \"code-snippet\": cửa sổ Terminal (codeSnippet.language, code) — chỉ cho chủ đề Công nghệ.");
            builder.AppendLine("Trường durationInFrames chỉ là ước lượng ở 30fps; thời lượng thật sẽ được tính theo giọng đọc.");
            builder.AppendLine();
            builder.AppendLine("VÍ DỤ ĐỊNH DẠNG (chỉ minh hoạ cấu trúc dữ liệu, KHÔNG sao chép nội dung, tiêu đề hay con số):");
            builder.AppendLine(HeroExample(profile));
            builder.AppendLine(profile.FlowchartScene);
            builder.AppendLine(profile.ComparisonScene);
            builder.AppendLine(profile.MetricScene);
            builder.AppendLine();
            builder.AppendLine("Đầu ra là JSON thuần: { \"title\", \"totalEstimatedSeconds\", \"scenes\": [...] }.");
            builder.Append(PromptSanitizer.DataBlockRule);
            return builder.ToString();
        }

        /// <summary>Trường dữ liệu bắt buộc của từng loại cảnh; nêu ngay ở từng dòng kế hoạch để model không bỏ sót.</summary>
        internal static string TypeRequirement(string type) => type switch
        {
            ExplainerSceneTypes.Flowchart => "BẮT BUỘC có flowchartNodes gồm 3-4 node (id, label, sublabel, icon, status)",
            ExplainerSceneTypes.CardComparison => "BẮT BUỘC có comparisonCards gồm đúng 2 thẻ (name, badge, icon, pros, cons), đúng một thẻ isWinner = true",
            ExplainerSceneTypes.MetricHighlight => "BẮT BUỘC có metric (value, label, description); value là con số lấy từ dữ kiện nghiên cứu",
            ExplainerSceneTypes.CodeSnippet => "BẮT BUỘC có codeSnippet (language, code)",
            _ => "có title, subtitle và tags gồm 2 từ khoá"
        };

        private static string HeroExample(CategoryProfile profile) =>
            "{ \"id\": \"s1\", \"type\": \"hero\", \"visualIcon\": \"" + profile.HeroIcon + "\", \"tags\": " + profile.DefaultTags
            + ", \"title\": \"Tiêu đề cảnh\", \"subtitle\": \"Mô tả ngắn\", \"narration\": \"Lời thoại\", \"durationInFrames\": 120 }";

        private static string BuildStoryboardUser(
            ExplainerRenderRequest request,
            CategoryProfile profile,
            IReadOnlyList<ScenePlan> plan,
            DurationRange range,
            IReadOnlyList<string>? previousIssues)
        {
            var dossier = request.ResearchDossier;
            var totalWords = NarrationHelper.TargetWords(range);
            var wordsPerScene = Math.Max(10, totalWords / plan.Count);
            var durationLabel = request.TargetDuration ?? DefaultStoryboardDuration;

            var builder = new StringBuilder();
            builder.AppendLine(PromptSanitizer.Wrap("Chủ đề", request.Topic, 300));
            builder.AppendLine($"Lĩnh vực: {request.Category ?? ExplainerCategories.Tech}");
            var title = PromptSanitizer.Wrap("Tiêu đề video mong muốn", request.Title, 200);
            if (title.Length > 0) builder.AppendLine(title);
            builder.AppendLine($"Tỷ lệ khung hình: {request.AspectRatio}");
            builder.AppendLine($"Thời lượng mục tiêu: {durationLabel} → tổng lời thoại khoảng {totalWords} từ (mỗi cảnh khoảng {wordsPerScene} từ).");

            var hook = PromptSanitizer.Wrap("Hook mở đầu đã chọn", request.SelectedHook, 300);
            if (hook.Length > 0) builder.AppendLine(hook);

            if (dossier != null)
            {
                AppendDossier(builder, dossier);
            }

            builder.AppendLine();
            builder.AppendLine($"KẾ HOẠCH PHÂN CẢNH ({plan.Count} cảnh, đúng thứ tự và đúng loại cảnh sau):");
            foreach (var scene in plan)
            {
                var chapter = string.IsNullOrWhiteSpace(scene.ChapterTitle)
                    ? string.Empty
                    : $" | chương: {PromptSanitizer.Clean(scene.ChapterTitle, 150)}"
                      + (string.IsNullOrWhiteSpace(scene.ChapterDescription)
                          ? string.Empty
                          : $" — {PromptSanitizer.Clean(scene.ChapterDescription, 250)}");
                builder.AppendLine($"Cảnh {scene.Index} (id \"s{scene.Index}\") | type: \"{scene.Type}\"{chapter} | khoảng {wordsPerScene} từ | {TypeRequirement(scene.Type)}");
            }

            return builder.ToString().TrimEnd() + FormatIssues(previousIssues);
        }

        private static void AppendDossier(StringBuilder builder, ExplainerResearchDossierDto dossier)
        {
            builder.AppendLine();
            builder.AppendLine("DỮ LIỆU NGHIÊN CỨU (nguồn sự thật duy nhất):");

            var definition = PromptSanitizer.Wrap("Định nghĩa cốt lõi", dossier.CoreDefinition, 500);
            if (definition.Length > 0) builder.AppendLine(definition);

            AppendList(builder, "Dữ kiện (phải dùng làm nội dung và số liệu cho các cảnh)", dossier.KeyFacts);
            AppendList(builder, "Hiểu lầm cần đính chính", dossier.CommonMisconceptions);
        }

        private static void AppendList(StringBuilder builder, string heading, IEnumerable<string>? items)
        {
            var cleaned = (items ?? Enumerable.Empty<string>())
                .Select(item => PromptSanitizer.Clean(item, MaxFactLength))
                .Where(item => item.Length > 0)
                .Take(MaxListItems)
                .ToList();
            if (cleaned.Count == 0) return;

            builder.AppendLine($"{heading}:");
            for (var i = 0; i < cleaned.Count; i++)
            {
                builder.AppendLine($"  {i + 1}. <du_lieu>{cleaned[i]}</du_lieu>");
            }
        }

        // ───────────────────────── Hồ sơ theo lĩnh vực ─────────────────────────

        /// <summary>Cấu hình prompt theo lĩnh vực. "general" dùng bộ icon và scene mẫu của "tech" (Tech phải khai báo trước General).</summary>
        private sealed record CategoryProfile(
            bool IsFinance,
            bool AllowsCode,
            string ResearchRole,
            string DirectorRole,
            string DefaultAudience,
            string DefaultTone,
            string AllowedIcons,
            string HeroIcon,
            string DefaultTags,
            string FlowchartScene,
            string ComparisonScene,
            string MetricScene)
        {
            private static readonly CategoryProfile Finance = new(
                IsFinance: true,
                AllowsCode: false,
                ResearchRole: "Bạn là Chuyên gia Nghiên cứu Tài chính, Đầu tư Thông minh & Kinh tế học hàng đầu thế giới (Phong cách Ray Dalio, Graham Stephan, Bloomberg Quicktake).",
                DirectorRole: "Bạn là Giám đốc Sáng tạo & Đồ họa Chuyển động (Creative Visual Director Agent) hàng đầu thế giới cho video giải thích Tài chính, Đầu tư Thông minh & Kinh tế học Remotion (Phong cách Ray Dalio, Bloomberg Quicktake, Graham Stephan).",
                DefaultAudience: "Nhà đầu tư cá nhân / Người đi làm",
                DefaultTone: "Thực tế, dẫn chứng số liệu, dễ hiểu",
                AllowedIcons: "\"dollar\", \"trending-up\", \"coins\", \"wallet\", \"barchart\", \"piechart\", \"percent\", \"landmark\", \"target\", \"shield\", \"zap\", \"sparkles\"",
                HeroIcon: "dollar",
                DefaultTags: "[\"TĂNG TRƯỞNG TÀI SẢN\", \"LÃI KÉP THÔNG MINH\"]",
                FlowchartScene: StoryboardTemplates.FinanceFlowchart,
                ComparisonScene: StoryboardTemplates.FinanceComparison,
                MetricScene: StoryboardTemplates.FinanceMetric);

            private static readonly CategoryProfile Tech = new(
                IsFinance: false,
                AllowsCode: true,
                ResearchRole: "Bạn là Giám đốc Nghiên cứu Nội dung Giáo dục & Giải thích Công nghệ hàng đầu thế giới (Phong cách 3Blue1Brown, Fireship).",
                DirectorRole: "Bạn là Giám đốc Sáng tạo & Đồ họa Chuyển động (Creative Visual Director Agent) hàng đầu thế giới cho video giải thích công nghệ Remotion (Phong cách 3Blue1Brown, Fireship, Kurzgesagt).",
                DefaultAudience: "Lập trình viên / Người yêu công nghệ",
                DefaultTone: "Cuốn hút, súc tích, thực tế",
                AllowedIcons: "\"server\", \"database\", \"cloud\", \"cpu\", \"boxes\", \"shield\", \"terminal\", \"network\", \"laptop\", \"zap\", \"sparkles\", \"activity\", \"layers\"",
                HeroIcon: "sparkles",
                DefaultTags: "[\"KIẾN TRÚC HIỆN ĐẠI\", \"TỐI ƯU HIỆU NĂNG\"]",
                FlowchartScene: StoryboardTemplates.TechFlowchart,
                ComparisonScene: StoryboardTemplates.TechComparison,
                MetricScene: StoryboardTemplates.TechMetric);

            private static readonly CategoryProfile General = new(
                IsFinance: false,
                AllowsCode: false,
                ResearchRole: "Bạn là Chuyên gia Phổ biến Kiến thức Khoa học & Đời sống hàng đầu thế giới (Phong cách Kurzgesagt, Vox, Ted-Ed).",
                DirectorRole: "Bạn là Giám đốc Sáng tạo & Đồ họa Chuyển động (Creative Visual Director Agent) hàng đầu thế giới cho video giải thích Khoa học & Đời sống Remotion (Phong cách Kurzgesagt, Vox, Ted-Ed).",
                DefaultAudience: "Đại chúng / Người tò mò học hỏi",
                DefaultTone: "Sinh động, lôi cuốn, kích thích tư duy",
                AllowedIcons: Tech.AllowedIcons,
                HeroIcon: Tech.HeroIcon,
                DefaultTags: Tech.DefaultTags,
                FlowchartScene: Tech.FlowchartScene,
                ComparisonScene: Tech.ComparisonScene,
                MetricScene: Tech.MetricScene);

            public static CategoryProfile For(string? category)
            {
                if (ExplainerCategories.IsFinance(category)) return Finance;
                return ExplainerCategories.IsGeneral(category) ? General : Tech;
            }
        }
    }
}
