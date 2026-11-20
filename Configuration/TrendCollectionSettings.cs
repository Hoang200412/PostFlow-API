using System.Collections.Generic;
using System.Linq;

namespace PostFlow.Configuration
{
    /// <summary>
    /// Cấu hình bộ thu thập dữ liệu video xu hướng (benchmark) từ YouTube Data API v3.
    /// </summary>
    public class TrendCollectionSettings
    {
        public const string SectionName = "TrendCollection";

        /// <summary>Tắt để dừng toàn bộ job thu thập định kỳ.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Mã vùng ISO 3166-1 alpha-2 dùng cho chart và search.</summary>
        public string RegionCode { get; set; } = "VN";

        /// <summary>Ngôn ngữ ưu tiên khi search (ISO 639-1).</summary>
        public string RelevanceLanguage { get; set; } = "vi";

        /// <summary>
        /// Danh mục nội bộ → YouTube videoCategoryId.
        /// 28 Science &amp; Technology, 25 News &amp; Politics, 22 People &amp; Blogs, 24 Entertainment, 27 Education.
        /// Để null làm mặc định (binder gộp key vào dictionary đã khởi tạo); dùng <see cref="CategoryMap"/> khi đọc.
        /// </summary>
        public Dictionary<string, string>? Categories { get; set; }

        public static readonly IReadOnlyDictionary<string, string> DefaultCategories = new Dictionary<string, string>
        {
            ["tech"] = "28",
            ["business"] = "25",
            ["lifestyle"] = "22",
            ["entertainment"] = "24",
            ["education"] = "27"
        };

        /// <summary>Danh mục đã chuẩn hóa (key chữ thường).</summary>
        public IReadOnlyDictionary<string, string> CategoryMap =>
            Categories is { Count: > 0 }
                ? Categories.ToDictionary(c => c.Key.Trim().ToLowerInvariant(), c => c.Value.Trim())
                : DefaultCategories;

        /// <summary>Chỉ lấy video đăng trong N ngày gần nhất.</summary>
        public int LookbackDays { get; set; } = 14;

        /// <summary>Số trang (mỗi trang tối đa 50 video) cho mỗi lượt search. Mỗi trang tốn 100 đơn vị quota.</summary>
        public int SearchPagesPerQuery { get; set; } = 1;

        /// <summary>Số video tối đa lấy từ chart mostPopular cho mỗi danh mục (tối đa 50, tốn 1 đơn vị).</summary>
        public int PopularMaxResults { get; set; } = 50;

        /// <summary>
        /// Ngân sách quota tự đặt cho mỗi ngày (quota mặc định của Google là 10.000/ngày, reset lúc 0h giờ Pacific).
        /// Giữ thấp hơn hạn mức để còn chỗ cho các tính năng khác dùng chung API key.
        /// </summary>
        public int DailyQuotaBudget { get; set; } = 3000;

        /// <summary>Video dài tối đa bao nhiêu giây thì được coi là Shorts (YouTube cho phép Shorts tới 3 phút).</summary>
        public int ShortsMaxSeconds { get; set; } = 180;

        /// <summary>
        /// Các mốc tuổi video (giờ kể từ lúc đăng) cần chụp snapshot số liệu. Để null làm mặc định vì configuration binder
        /// nối thêm phần tử vào list đã khởi tạo thay vì thay thế; dùng <see cref="Milestones"/> khi đọc.
        /// </summary>
        public List<int>? SnapshotMilestonesHours { get; set; }

        public static readonly IReadOnlyList<int> DefaultMilestones = new[] { 24, 72, 168 };

        /// <summary>Danh sách mốc đã chuẩn hóa (dương, không trùng, tăng dần).</summary>
        public IReadOnlyList<int> Milestones =>
            SnapshotMilestonesHours?.Where(m => m > 0).Distinct().OrderBy(m => m).ToList() is { Count: > 0 } configured
                ? configured
                : DefaultMilestones;

        /// <summary>Ngừng theo dõi video sau N ngày kể từ lúc đăng.</summary>
        public int TrackingDays { get; set; } = 14;
    }
}
