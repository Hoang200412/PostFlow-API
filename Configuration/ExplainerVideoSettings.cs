namespace PostFlow.Configuration
{
    /// <summary>
    /// Đường dẫn công cụ ngoài dùng cho pipeline Explainer Video (section "ExplainerVideo").
    /// Để trống để dùng giá trị mặc định (thư mục làm việc hiện tại của ứng dụng).
    /// </summary>
    public class ExplainerVideoSettings
    {
        public const string SectionName = "ExplainerVideo";

        /// <summary>Thư mục dự án RemotionEngine.</summary>
        public string? RemotionEnginePath { get; set; }

        /// <summary>Đường dẫn script Python edge_tts_aligner.py.</summary>
        public string? EdgeTtsAlignerScriptPath { get; set; }

        /// <summary>Thư mục chứa npx.cmd (nếu Node.js không nằm trong PATH).</summary>
        public string? NodeDirectory { get; set; }

        /// <summary>Tên hoặc đường dẫn file thực thi Python.</summary>
        public string PythonExecutable { get; set; } = "python";
    }
}
