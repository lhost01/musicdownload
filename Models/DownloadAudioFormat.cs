namespace Musicbox.Models;

public enum DownloadAudioFormat
{
    Mp3,
    Flac
}

public sealed class DownloadFormatOption
{
    public DownloadAudioFormat Format { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public override string ToString() => DisplayName;
}
