using System;

namespace PostFlow.Models
{
    public class SystemSetting
    {
        public string Key { get; set; } = string.Empty; // Primary Key
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
