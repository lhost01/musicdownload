using CommunityToolkit.Mvvm.ComponentModel;

namespace Musicbox.Models;

public enum ConversionStatus
{
    Pending,
    Converting,
    Completed,
    Failed,
    Cancelled
}

public partial class NcmFileInfo : ObservableObject
{
    [ObservableProperty]
    private Guid id = Guid.NewGuid();

    [ObservableProperty]
    private string fileName = string.Empty;

    [ObservableProperty]
    private string fullPath = string.Empty;

    [ObservableProperty]
    private long fileSize;

    [ObservableProperty]
    private ConversionStatus status = ConversionStatus.Pending;

    [ObservableProperty]
    private int progress;

    [ObservableProperty]
    private string outputPath = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private DateTime addedTime = DateTime.Now;

    public string FileSizeText => FormatBytes(FileSize);

    public string StatusText => Status switch
    {
        ConversionStatus.Pending => "等待中",
        ConversionStatus.Converting => "转换中",
        ConversionStatus.Completed => "已完成",
        ConversionStatus.Failed => "失败",
        ConversionStatus.Cancelled => "已取消",
        _ => "未知"
    };

    partial void OnFileSizeChanged(long value) => OnPropertyChanged(nameof(FileSizeText));

    partial void OnStatusChanged(ConversionStatus value) => OnPropertyChanged(nameof(StatusText));

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
