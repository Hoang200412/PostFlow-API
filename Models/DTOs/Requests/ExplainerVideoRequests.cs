using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests
{
    public class ExplainerResearchRequest
    {
        [Required(ErrorMessage = "Chủ đề cần giải thích không được để trống.")]
        public string Topic { get; set; } = string.Empty;

        public string? Category { get; set; } = "tech"; // "tech", "finance", "general"

        public string? TargetAudience { get; set; } = "Người xem phổ thông / Lập trình viên trẻ";

        public string? Tone { get; set; } = "Chuyên nghiệp, cuốn hút, giàu dẫn chứng thực tế";

        public string? Language { get; set; } = "vi";
    }

    public class ExplainerRenderRequest
    {
        [Required(ErrorMessage = "Chủ đề không được để trống.")]
        public string Topic { get; set; } = string.Empty;

        public string? Category { get; set; } = "tech"; // "tech", "finance", "general"

        public string? Title { get; set; }

        public string? TargetDuration { get; set; } = "30-45 giây";

        public string Voice { get; set; } = "vi-VN-NamMinhNeural";

        public string AspectRatio { get; set; } = "16:9"; // "16:9" or "9:16"

        public string Rate { get; set; } = "+0%";

        public string Pitch { get; set; } = "+0Hz";

        /// <summary>Kết quả nghiên cứu ở bước trước; khi có, storyboard bám theo dữ kiện, hook và dàn ý trong đó.</summary>
        public ExplainerResearchDossierDto? ResearchDossier { get; set; }

        /// <summary>Hook mở đầu người dùng đã chọn ở bước nghiên cứu.</summary>
        public string? SelectedHook { get; set; }

        public List<ExplainerSceneDto>? CustomScenes { get; set; }
    }

    public class ExplainerResearchDossierDto
    {
        public string Topic { get; set; } = string.Empty;
        public string CoreDefinition { get; set; } = string.Empty;
        public List<string> KeyFacts { get; set; } = new();
        public List<string> CommonMisconceptions { get; set; } = new();
        public List<string> RecommendedHooks { get; set; } = new();
        public List<ExplainerChapterDto> Chapters { get; set; } = new();
    }

    public class ExplainerChapterDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LayoutType { get; set; } = "hero"; // hero, flowchart, card-comparison, metric-highlight, code-snippet
    }

    public class ExplainerSceneDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "hero";
        public string? Category { get; set; } = "tech";
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string Narration { get; set; } = string.Empty;
        public int DurationInFrames { get; set; } = 150;
        public string? VisualIcon { get; set; }
        public string? VisualTheme { get; set; }
        public string? ImageUrl { get; set; }
        public string? Icon { get; set; }
        public List<string>? Tags { get; set; }
        public List<FlowchartNodeDto>? FlowchartNodes { get; set; }
        public List<CardComparisonItemDto>? ComparisonCards { get; set; }
        public MetricItemDto? Metric { get; set; }
        public CodeSnippetDto? CodeSnippet { get; set; }
    }

    public class FlowchartNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Sublabel { get; set; }
        public string? Icon { get; set; }
        public string? Status { get; set; }
    }

    public class CardComparisonItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Badge { get; set; }
        public string? Icon { get; set; }
        public List<string> Pros { get; set; } = new();
        public List<string>? Cons { get; set; } = new();
        public bool IsWinner { get; set; }
    }

    public class MetricItemDto
    {
        public string Value { get; set; } = string.Empty;
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class CodeSnippetDto
    {
        public string Language { get; set; } = "dockerfile";
        public string Code { get; set; } = string.Empty;
    }
}

