namespace PostFlow.Configuration;

public class PostFlowTtsSettings
{
    public const string SectionName = "PostFlowTts";

    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://localhost:8001";
    public string DefaultVoice { get; set; } = "diem_trinh";
    public int TimeoutSeconds { get; set; } = 120;
}
