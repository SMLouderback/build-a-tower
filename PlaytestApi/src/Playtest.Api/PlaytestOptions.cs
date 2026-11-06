namespace Playtest.Api;

public sealed class PlaytestOptions
{
    public string DataDirectory { get; set; } = "/data";
    public string ZipFileName { get; set; } = "Build-A-Tower.zip";
    public string OperatorEmail { get; set; } = "escapemobileproductions@gmail.com";
    public string DownloadPage { get; set; } = "https://escapeproductions.biz/#download";
    /// <summary>Public site origin used to build confirmation links (no trailing slash).</summary>
    public string PublicBaseUrl { get; set; } = "https://escapeproductions.biz";
    public int ConfirmTokenHours { get; set; } = 48;
}
