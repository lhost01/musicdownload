using CommunityToolkit.Mvvm.ComponentModel;

namespace Musicbox.Models;

public enum DownloadStatus
{
    Pending,
    FetchingInfo,
    Downloading,
    Converting,
    Completed,
    Failed,
    Cancelled
}

public partial class DownloadTaskInfo : ObservableObject
{
    [ObservableProperty]
    private Guid id = Guid.NewGuid();

    [ObservableProperty]
    private string songId = string.Empty;

    [ObservableProperty]
    private string songName = string.Empty;

    [ObservableProperty]
    private string artist = string.Empty;

    [ObservableProperty]
    private string album = string.Empty;

    [ObservableProperty]
    private DownloadStatus status = DownloadStatus.Pending;

    [ObservableProperty]
    private int progress;

    [ObservableProperty]
    private string outputPath = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private DateTime createdTime = DateTime.Now;

    [ObservableProperty]
    private DateTime? completedTime;

    [ObservableProperty]
    private long fileSize;

    [ObservableProperty]
    private long downloadSpeed;

    public string FileSizeText => FormatBytes(FileSize);

    public string DownloadSpeedText => DownloadSpeed > 0 ? $"{FormatBytes(DownloadSpeed)}/s" : string.Empty;

    public string StatusText => Status switch
    {
        DownloadStatus.Pending => "等待中",
        DownloadStatus.FetchingInfo => "获取信息中",
        DownloadStatus.Downloading => "下载中",
        DownloadStatus.Converting => "转换中",
        DownloadStatus.Completed => "已完成",
        DownloadStatus.Failed => "失败",
        DownloadStatus.Cancelled => "已取消",
        _ => "未知"
    };

    partial void OnStatusChanged(DownloadStatus value) => OnPropertyChanged(nameof(StatusText));

    partial void OnFileSizeChanged(long value) => OnPropertyChanged(nameof(FileSizeText));

    partial void OnDownloadSpeedChanged(long value) => OnPropertyChanged(nameof(DownloadSpeedText));

    private static string FormatBytes(long bytes)
    {
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        return bytes switch
        {
            >= gb => $"{bytes / (double)gb:F2} GB",
            >= mb => $"{bytes / (double)mb:F2} MB",
            >= kb => $"{bytes / (double)kb:F2} KB",
            _ => $"{bytes} B"
        };
    }
}
