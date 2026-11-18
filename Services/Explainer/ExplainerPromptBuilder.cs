using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Explainer
{
    /// <summary>Dựng prompt gửi AI cho bước nghiên cứu chủ đề và thiết kế storyboard.</summary>
    public static class ExplainerPromptBuilder
    {
        private const string DefaultStoryboardDuration = "30-45 giây";

        public static string BuildResearchPrompt(ExplainerResearchRequest request)
        {
            var profile = CategoryProfile.For(request.Category);

            return $$"""
                {{profile.ResearchRole}}
                Hãy nghiên cứu chuyên sâu về chủ đề: "{{request.Topic}}".
                Đối tượng khán giả: {{request.TargetAudience ?? profile.DefaultAudience}}
                Văn phong: {{request.Tone ?? profile.DefaultTone}}

                BẮT BUỘC TRẢ VỀ JSON THUẦN TÚY (application/json) theo định dạng sau:
                {{ResearchJsonSchema}}
                """;
        }

        public static string BuildStoryboardPrompt(ExplainerRenderRequest request)
        {
            var profile = CategoryProfile.For(request.Category);
            var financeNotice = profile.IsFinance
                ? "CHÚ Ý: Với chủ đề Tài chính, TUYỆT ĐỐI KHÔNG dùng loại cảnh 'code-snippet'. Hãy dùng 'flowchart', 'card-comparison', 'metric-highlight', hoặc 'hero'."
                : string.Empty;

            return $$"""
                {{profile.DirectorRole}}
                Chủ đề: "{{request.Topic}}"
                Lĩnh vực: {{request.Category ?? ExplainerCategories.Tech}}
                Tỷ lệ khung hình: {{request.AspectRatio}}
                Thời lượng dự kiến: {{request.TargetDuration ?? DefaultStoryboardDuration}}

                Hãy thiết kế kịch bản gồm 3 đến 5 phân cảnh (Scenes) phong cách Motion Graphics hiện đại, TUYỆT ĐỐI KHÔNG để khung hình trống hay chỉ có chữ thô sơ. BẮT BUỘC mỗi phân cảnh phải có biểu tượng trực quan động (visualIcon), thẻ tag động (tags) gồm 2 từ khóa nổi bật của chủ đề, và cấu trúc phân cảnh trực quan.
                {{financeNotice}}

                Danh sách biểu tượng visualIcon hợp lệ:
                - {{profile.AllowedIcons}}.

                Các loại Scene Type chuẩn (viết thường):
                1. "hero": Mở đầu hoặc kết luận cuốn hút với biểu tượng trung tâm lớn phát sáng, tiêu đề, phụ đề và mảng tags gồm 2 từ khóa ngắn gọn đại diện chủ đề.
                2. "flowchart": Sơ đồ luồng xử lý hoặc chu trình gồm 3-4 node, mỗi node có icon riêng, kết nối bởi tia năng lượng chuyển động.
                3. "card-comparison": So sánh 2 bên (Cách cũ vs Cách mới / Giữ tiền vs Đầu tư), có badge, icon, ưu điểm (pros), nhược điểm (cons), đánh dấu bên chiến thắng (isWinner: true).
                4. "metric-highlight": Điểm nhấn số liệu đột phá kèm đồng hồ đo vòng tròn SVG phát sáng.
                5. "code-snippet": Cửa sổ dòng lệnh Terminal (chỉ áp dụng cho chủ đề Công nghệ).

                BẮT BUỘC TRẢ VỀ ĐÚNG ĐỊNH DẠNG JSON (application/json):
                {
                  "topic": "{{request.Topic}}",
                  "title": "Tiêu đề video cuốn hút",
                  "aspectRatio": "{{request.AspectRatio}}",
                  "totalEstimatedSeconds": 35,
                  "scenes": [
                    {
                      "id": "s1",
                      "type": "hero",
                      "visualIcon": "{{profile.HeroIcon}}",
                      "tags": {{profile.DefaultTags}},
                      "title": "Tiêu đề phân cảnh mở đầu",
                      "subtitle": "Mô tả ngắn gọn gợi mở vấn đề",
                      "narration": "Lời thoại đọc dẫn dắt bằng tiếng Việt",
                      "durationInFrames": 120
                    },
                    {{profile.FlowchartScene}},
                    {{profile.ComparisonScene}},
                    {{profile.MetricScene}}
                  ]
                }
                """;
        }

        /// <summary>Cấu hình prompt theo lĩnh vực. "general" dùng bộ icon và scene mẫu của "tech" (Tech phải khai báo trước General).</summary>
        private sealed record CategoryProfile(
            bool IsFinance,
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

        private const string ResearchJsonSchema = """
            {
              "coreDefinition": "Định nghĩa súc tích, dễ hiểu nhất về khái niệm này (khoảng 2 câu)",
              "keyFacts": [
                "Dữ kiện/Số liệu thực tế 1",
                "Dữ kiện/Số liệu thực tế 2",
                "Dữ kiện/Số liệu thực tế 3"
              ],
              "commonMisconceptions": [
                "Hiểu lầm phổ biến 1 mà 90% mọi người mắc phải",
                "Hiểu lầm phổ biến 2"
              ],
              "recommendedHooks": [
                "Câu hook 1 (Gợi mở vấn đề bất ngờ trong 3s)",
                "Câu hook 2 (Câu hỏi khiêu khích)",
                "Câu hook 3 (Tuyên bố gây sốc về dữ liệu)"
              ],
              "suggestedChapters": [
                {
                  "title": "Chương 1: Mở đầu & Đặt vấn đề",
                  "description": "Nêu bật nỗi đau hoặc câu hỏi lớn",
                  "layoutType": "hero"
                },
                {
                  "title": "Chương 2: Cơ chế hoạt động cốt lõi",
                  "description": "Giải thích cách vận hành qua sơ đồ luồng",
                  "layoutType": "flowchart"
                },
                {
                  "title": "Chương 3: So sánh trực quan",
                  "description": "Đặt lên bàn cân với giải pháp thay thế",
                  "layoutType": "card-comparison"
                },
                {
                  "title": "Chương 4: Điểm nhấn số liệu & Kết luận",
                  "description": "Đưa ra con số ấn tượng và bài học rút ra",
                  "layoutType": "metric-highlight"
                }
              ]
            }
            """;
    }
}
