namespace PostFlow.Services.Explainer
{
    /// <summary>Scene mẫu (JSON) chèn vào prompt storyboard để AI bám đúng cấu trúc dữ liệu.</summary>
    internal static class StoryboardTemplates
    {
        public const string TechFlowchart = """
            {
              "id": "s2",
              "type": "flowchart",
              "visualIcon": "network",
              "title": "Cơ Chế Hoạt Động Cốt Lõi",
              "subtitle": "Quy trình xử lý dữ liệu tự động",
              "narration": "Lời thoại giải thích luồng hoạt động từng bước",
              "durationInFrames": 150,
              "flowchartNodes": [
                { "id": "1", "label": "Client", "sublabel": "Gửi request", "icon": "laptop", "status": "normal" },
                { "id": "2", "label": "API Gateway", "sublabel": "Bảo mật & Điều phối", "icon": "shield", "status": "active" },
                { "id": "3", "label": "Core Engine", "sublabel": "Xử lý siêu tốc", "icon": "cpu", "status": "highlight" },
                { "id": "4", "label": "Database", "sublabel": "Lưu trữ an toàn", "icon": "database", "status": "normal" }
              ]
            }
            """;

        public const string FinanceFlowchart = """
            {
              "id": "s2",
              "type": "flowchart",
              "visualIcon": "trending-up",
              "title": "Quy Trình Quản Lý & Tăng Trưởng Vốn",
              "subtitle": "Dòng tiền luân chuyển thông minh",
              "narration": "Lời thoại phân tích các bước vận hành dòng tiền",
              "durationInFrames": 150,
              "flowchartNodes": [
                { "id": "1", "label": "Thu Nhập", "sublabel": "Dòng tiền vào", "icon": "wallet", "status": "normal" },
                { "id": "2", "label": "Chi Phí Cố Định", "sublabel": "Quy tắc 50%", "icon": "creditcard", "status": "normal" },
                { "id": "3", "label": "Quỹ Đầu Tư", "sublabel": "Tích lũy tài sản", "icon": "trending-up", "status": "highlight" },
                { "id": "4", "label": "Tự Do Tài Chính", "sublabel": "Lợi nhuận kép", "icon": "coins", "status": "active" }
              ]
            }
            """;

        public const string TechComparison = """
            {
              "id": "s3",
              "type": "card-comparison",
              "visualIcon": "layers",
              "title": "So Sánh Hiệu Năng",
              "subtitle": "Sự khác biệt vượt trội",
              "narration": "Lời thoại đối chiếu giữa phương pháp cũ và kiến trúc mới",
              "durationInFrames": 150,
              "comparisonCards": [
                {
                  "name": "Giải Pháp Cũ",
                  "badge": "Truyền Thống",
                  "icon": "server",
                  "pros": ["Dễ bắt đầu"],
                  "cons": ["Độ trễ cao", "Tốn tài nguyên"],
                  "isWinner": false
                },
                {
                  "name": "Giải Pháp Mới",
                  "badge": "Đột Phá",
                  "icon": "zap",
                  "pros": ["Nhanh gấp 10 lần", "Tự động mở rộng", "Tiết kiệm 80% chi phí"],
                  "cons": [],
                  "isWinner": true
                }
              ]
            }
            """;

        public const string FinanceComparison = """
            {
              "id": "s3",
              "type": "card-comparison",
              "visualIcon": "piechart",
              "title": "So Sánh Hiệu Quả Tài Chính",
              "subtitle": "Sự khác biệt sau 10 năm",
              "narration": "Lời thoại đối chiếu giữa giữ tiền mặt và đầu tư kỷ luật",
              "durationInFrames": 150,
              "comparisonCards": [
                {
                  "name": "Giữ Tiền Mặt",
                  "badge": "Truyền Thống",
                  "icon": "wallet",
                  "pros": ["An toàn ngắn hạn"],
                  "cons": ["Bị lạm phát bào mòn", "Mất giá theo thời gian"],
                  "isWinner": false
                },
                {
                  "name": "Đầu Tư Kỷ Luật",
                  "badge": "Đột Phá",
                  "icon": "trending-up",
                  "pros": ["Hưởng trọn sức mạnh lãi kép", "Bảo vệ tài sản trước lạm phát", "Tự do tài chính sớm"],
                  "cons": [],
                  "isWinner": true
                }
              ]
            }
            """;

        public const string TechMetric = """
            {
              "id": "s4",
              "type": "metric-highlight",
              "visualIcon": "zap",
              "title": "Hiệu Quả Thực Tế Đột Phá",
              "narration": "Lời thoại nhấn mạnh thành quả và số liệu ấn tượng",
              "durationInFrames": 120,
              "metric": {
                "value": "10x",
                "prefix": "",
                "suffix": "",
                "label": "Tốc Độ Xử Lý Nhanh Hơn",
                "description": "Giảm thiểu tối đa độ trễ và tối ưu chi phí vận hành máy chủ."
              }
            }
            """;

        public const string FinanceMetric = """
            {
              "id": "s4",
              "type": "metric-highlight",
              "visualIcon": "coins",
              "title": "Sức Mạnh Của Lãi Kép",
              "narration": "Lời thoại nhấn mạnh thành quả tăng trưởng tài sản",
              "durationInFrames": 120,
              "metric": {
                "value": "72",
                "prefix": "Quy tắc ",
                "suffix": "",
                "label": "Số Năm Nhân Đôi Tài Sản",
                "description": "Lấy 72 chia cho lãi suất hàng năm sẽ biết chính xác thời gian tài sản của bạn tăng gấp đôi."
              }
            }
            """;
    }
}
