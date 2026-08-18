using System;

namespace PostFlow.Models
{
    public class ApiUsageLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty; // "Gemini", "Pexels"
        public decimal CostEstimate { get; set; }
        public int CallCount { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property
        public virtual User? User { get; set; }
    }
}
