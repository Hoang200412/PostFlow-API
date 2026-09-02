namespace PostFlow.Models.DTOs.Requests
{
    public class UpdateSettingRequest
    {
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
