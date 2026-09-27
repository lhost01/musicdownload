namespace Musicbox.Models;

public sealed class SongSearchResult
{
    public string SongId { get; init; } = string.Empty;

    public string SongName { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public string Album { get; init; } = string.Empty;

    public long Duration { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(Artist)
        ? SongName
        : $"{SongName} - {Artist}";

    public string Subtitle => string.IsNullOrWhiteSpace(Album)
        ? $"ID {SongId}"
        : $"{Album} · ID {SongId}";

    public string DurationText
    {
        get
        {
            var duration = TimeSpan.FromMilliseconds(Duration);
            return duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss")
                : duration.ToString(@"m\:ss");
        }
    }
}
