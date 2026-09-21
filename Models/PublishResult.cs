namespace PostFlow.Models
{
    public class PublishResult
    {
        public bool IsSuccess { get; set; }
        public string? VideoId { get; set; }
        public string? VideoUrl { get; set; }
        public string? ErrorMessage { get; set; }

        public static PublishResult Success(string videoId, string videoUrl) => new() 
        { 
            IsSuccess = true, 
            VideoId = videoId, 
            VideoUrl = videoUrl 
        };

        public static PublishResult Failed(string errorMessage) => new() 
        { 
            IsSuccess = false, 
            ErrorMessage = errorMessage 
        };
    }
}
