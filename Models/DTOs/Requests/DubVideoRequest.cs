namespace PostFlow.Models.DTOs.Requests
{
    public class DubVideoRequest
    {
        public string VideoAssetId { get; set; } = string.Empty;
        public string? AudioUrl { get; set; }
        public string? AudioObjectKey { get; set; }
        public string? Text { get; set; }
        public string? Voice { get; set; }
        public string? Rate { get; set; }
        public string? Pitch { get; set; }
        public string AudioMode { get; set; } = "mix"; // "replace" (thay thế) hoặc "mix" (hòa trộn)
        public string? Title { get; set; }

        // Timeline & Multi-track controls
        public double StartTimeSeconds { get; set; } = 0.0; // Offset bắt đầu giọng đọc (giây)
        public double VoiceVolume { get; set; } = 1.0; // Âm lượng giọng đọc: 0.0 -> 2.0 (mặc định 1.0)
        public double OriginalAudioVolume { get; set; } = 0.25; // Âm lượng tiếng gốc video: 0.0 -> 1.0 (0.0 = Mute)

        // Background Music (Track 3)
        public string? BgmUrl { get; set; } // URL nhạc nền
        public string? BgmObjectKey { get; set; } // ObjectKey nếu tải lên R2
        public double BgmVolume { get; set; } = 0.2; // Âm lượng nhạc nền: 0.0 -> 1.0 (mặc định 0.2)
        public double BgmStartTimeSeconds { get; set; } = 0.0; // Offset bắt đầu nhạc nền (giây)
    }
}

