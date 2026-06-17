using TagLib;

namespace Musicbox.Models;

public sealed class PlaylistSongInfo
{
    public string FileName { get; init; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public string Album { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public TimeSpan Duration { get; init; }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Title)
            ? string.IsNullOrWhiteSpace(Artist) ? Title : $"{Title} - {Artist}"
            : FileName;

    public string ArtistAlbumText =>
        !string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Album)
            ? $"{Artist} · {Album}"
            : string.IsNullOrWhiteSpace(Artist) ? Album : Artist;

    public string FileSizeText => FileSize < 1024 * 1024
        ? $"{FileSize / 1024d:F1} KB"
        : $"{FileSize / (1024d * 1024d):F1} MB";

    public string DurationText => Duration.TotalHours >= 1 ? Duration.ToString(@"h\:mm\:ss") : Duration.ToString(@"m\:ss");

    public static PlaylistSongInfo? FromFilePath(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            return null;
        }

        string title = string.Empty;
        string artist = string.Empty;
        string album = string.Empty;
        TimeSpan duration = TimeSpan.Zero;

        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            title = tagFile.Tag.Title ?? string.Empty;
            artist = tagFile.Tag.Performers is { Length: > 0 }
                ? string.Join(", ", tagFile.Tag.Performers)
                : string.Empty;
            album = tagFile.Tag.Album ?? string.Empty;
            duration = tagFile.Properties.Duration;
        }
        catch
        {
        }

        return new PlaylistSongInfo
        {
            FileName = fileInfo.Name,
            FilePath = fileInfo.FullName,
            FileSize = fileInfo.Length,
            Title = title,
            Artist = artist,
            Album = album,
            Duration = duration
        };
    }
}
