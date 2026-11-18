using Microsoft.Extensions.Options;
using PostFlow.Configuration;

namespace PostFlow.Services.Explainer
{
    /// <summary>
    /// Xác định vị trí RemotionEngine và script Edge-TTS: ưu tiên cấu hình, sau đó thư mục làm việc,
    /// cuối cùng là đường dẫn dự phòng của máy dev.
    /// </summary>
    public class ExplainerToolLocator
    {
        // Đường dẫn dự phòng cũ của máy dev. Nên cấu hình qua appsettings (section "ExplainerVideo") thay vì dựa vào đây.
        private const string LegacyDevRoot = @"D:\Đồ án\PostFlow-API";

        private readonly ExplainerVideoSettings _settings;

        public ExplainerToolLocator(IOptions<ExplainerVideoSettings> settings)
        {
            _settings = settings.Value;
        }

        public string PythonExecutable => _settings.PythonExecutable;

        public string? NodeDirectory =>
            !string.IsNullOrWhiteSpace(_settings.NodeDirectory) && Directory.Exists(_settings.NodeDirectory)
                ? _settings.NodeDirectory
                : null;

        public string RemotionEnginePath => Resolve(
            _settings.RemotionEnginePath,
            Path.Combine(Directory.GetCurrentDirectory(), "RemotionEngine"),
            Path.Combine(LegacyDevRoot, "RemotionEngine"),
            Directory.Exists);

        public string EdgeTtsAlignerScriptPath => Resolve(
            _settings.EdgeTtsAlignerScriptPath,
            Path.Combine(Directory.GetCurrentDirectory(), "Tools", "edge_tts_aligner.py"),
            Path.Combine(LegacyDevRoot, "Tools", "edge_tts_aligner.py"),
            File.Exists);

        private static string Resolve(string? configured, string local, string legacy, Func<string, bool> exists)
        {
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            return exists(local) ? local : legacy;
        }
    }
}
