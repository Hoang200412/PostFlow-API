namespace PostFlow.Models
{
    public enum PlatformType
    {
        YouTube,
        TikTok,
        FacebookReels
    }

    public enum UploadStatus
    {
        Pending,
        Processing,
        Completed, // Acts as POSTED
        Failed,
        Cancelled,
        DeadLetter,
        PostDeleted
    }
}
