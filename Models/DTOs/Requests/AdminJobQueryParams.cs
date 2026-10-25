namespace PostFlow.Models.DTOs.Requests
{
    public class AdminJobQueryParams
    {
        public string? Status { get; set; }
        public string? Platform { get; set; }
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
    }
}
