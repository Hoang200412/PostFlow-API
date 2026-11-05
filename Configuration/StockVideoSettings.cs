namespace PostFlow.Configuration;

public class StockVideoSettings
{
    public string Provider { get; set; } = "Pexels";
    public string PexelsApiKey { get; set; } = string.Empty;
    public int SearchCacheMinutes { get; set; } = 1440;
    public int DownloadTimeoutSeconds { get; set; } = 60;
    public int SearchTimeoutSeconds { get; set; } = 25;
    public int SearchRetryCount { get; set; } = 1;
    public int MaxSearchResults { get; set; } = 20;
    public int MaxMusicSizeMb { get; set; } = 20;
    public int MaxMediaLibraryVideoSizeMb { get; set; } = 200;
}
