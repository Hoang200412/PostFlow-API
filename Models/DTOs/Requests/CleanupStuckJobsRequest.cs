namespace PostFlow.Models.DTOs.Requests
{
    public class CleanupStuckJobsRequest
    {
        public int StuckMinutes { get; set; } = 15;
    }
}
