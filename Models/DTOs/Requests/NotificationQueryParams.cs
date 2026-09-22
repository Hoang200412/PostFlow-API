namespace PostFlow.Models.DTOs.Requests
{
    public class NotificationQueryParams
    {
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
        public bool? UnreadOnly { get; set; }
    }
}
