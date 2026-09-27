using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;
using Musicbox.Helpers;

namespace Musicbox.Models;

public partial class PlaylistInfo : ObservableObject
{
    private const string MarkerType = "musicbox-playlist";
    private static readonly string[] SupportedCoverExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];
    public const string MarkerFileName = "musicbox.playlist.json";
    public const string LegacyMarkerFileName = ".musicbox.playlist";
    public const string CoverFileName = "cover.jpg";
    public const string CoverFileBaseName = "cover";

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string folderPath = string.Empty;

    [ObservableProperty]
    private string coverImagePath = string.Empty;

    [ObservableProperty]
    private Bitmap? coverBitmap;

    [ObservableProperty]
    private DateTime createdTime = DateTime.Now;

    public ObservableCollection<PlaylistSongInfo> Songs { get; } = [];

    public int SongCount => Songs.Count;

    public string SongCountText => $"{SongCount} 首歌曲";

    public string CreatedTimeText => CreatedTime.ToString("yyyy-MM-dd HH:mm");

    public bool HasCustomCover => !string.IsNullOrWhiteSpace(CoverImagePath) && File.Exists(CoverImagePath);

    public void AddSong(PlaylistSongInfo song)
    {
        if (Songs.Any(existing => string.Equals(existing.FilePath, song.FilePath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Songs.Add(song);
        RaiseSongMetaChanged();
    }

    public void RemoveSong(PlaylistSongInfo song)
    {
        Songs.Remove(song);
        RaiseSongMetaChanged();
    }

    public void ReloadSongs()
    {
        Songs.Clear();
        if (!Directory.Exists(FolderPath))
        {
            RaiseSongMetaChanged();
            return;
        }

        foreach (var file in Directory.GetFiles(FolderPath)
            .Where(path => IsAudioFile(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var song = PlaylistSongInfo.FromFilePath(file);
            if (song is not null)
            {
                Songs.Add(song);
            }
        }

        CoverImagePath = ResolveCoverImagePath(FolderPath) ?? string.Empty;
        RaiseSongMetaChanged();
    }

    public static bool IsPlaylistFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return false;
        }

        var markerPath = Path.Combine(folderPath, MarkerFileName);
        if (TryReadMarkerMetadata(markerPath, out _))
        {
            return true;
        }

        var legacyMarkerPath = Path.Combine(folderPath, LegacyMarkerFileName);
        return TryReadLegacyMarker(legacyMarkerPath);
    }

    public static bool TryEnsureMarker(string folderPath, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            Directory.CreateDirectory(folderPath);

            var markerPath = Path.Combine(folderPath, MarkerFileName);
            var existingCreatedAt = TryReadMarkerMetadata(markerPath, out var existingMetadata)
                ? existingMetadata!.CreatedAt
                : DateTime.Now;

            var markerPayload = JsonSerializer.Serialize(new PlaylistMarkerMetadata
            {
                Type = MarkerType,
                Version = 2,
                Name = Path.GetFileName(folderPath),
                CreatedAt = existingCreatedAt
            }, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(markerPath, markerPayload);

            try
            {
                var attributes = File.GetAttributes(markerPath);
                File.SetAttributes(markerPath, attributes | FileAttributes.Hidden);
            }
            catch
            {
                // 某些环境可能不支持隐藏属性，保持标记文件可读写即可。
            }

            var legacyMarkerPath = Path.Combine(folderPath, LegacyMarkerFileName);
            if (File.Exists(legacyMarkerPath))
            {
                File.Delete(legacyMarkerPath);
            }

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            errorMessage = $"没有权限在 `{folderPath}` 写入歌单标记文件";
            return false;
        }
        catch (IOException)
        {
            errorMessage = $"无法在 `{folderPath}` 写入歌单标记文件";
            return false;
        }
        catch
        {
            errorMessage = $"写入 `{folderPath}` 的歌单标记文件时发生异常";
            return false;
        }
    }

    public static bool IsAudioFile(string extension)
    {
        return extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".flac", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ncm", StringComparison.OrdinalIgnoreCase);
    }

    public static string? ResolveCoverImagePath(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return null;
        }

        var legacyPath = Path.Combine(folderPath, CoverFileName);
        if (File.Exists(legacyPath))
        {
            return legacyPath;
        }

        foreach (var extension in SupportedCoverExtensions)
        {
            var path = Path.Combine(folderPath, $"{CoverFileBaseName}{extension}");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public static void DeleteExistingCoverFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        var candidates = SupportedCoverExtensions
            .Select(extension => Path.Combine(folderPath, $"{CoverFileBaseName}{extension}"))
            .Append(Path.Combine(folderPath, CoverFileName))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    partial void OnCoverImagePathChanged(string value)
    {
        CoverBitmap = ImageSourceHelper.LoadBitmap(value);
        OnPropertyChanged(nameof(HasCustomCover));
    }

    private void RaiseSongMetaChanged()
    {
        OnPropertyChanged(nameof(SongCount));
        OnPropertyChanged(nameof(SongCountText));
        OnPropertyChanged(nameof(HasCustomCover));
    }

    private static bool TryReadMarkerMetadata(string markerPath, out PlaylistMarkerMetadata? metadata)
    {
        metadata = null;
        if (!File.Exists(markerPath))
        {
            return false;
        }

        try
        {
            metadata = JsonSerializer.Deserialize<PlaylistMarkerMetadata>(File.ReadAllText(markerPath));
            return metadata is not null &&
                   string.Equals(metadata.Type, MarkerType, StringComparison.OrdinalIgnoreCase) &&
                   metadata.Version >= 1;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadLegacyMarker(string markerPath)
    {
        if (!File.Exists(markerPath))
        {
            return false;
        }

        try
        {
            return string.Equals(
                File.ReadAllText(markerPath).Trim(),
                MarkerType,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private sealed class PlaylistMarkerMetadata
    {
        public string Type { get; init; } = string.Empty;

        public int Version { get; init; }

        public string Name { get; init; } = string.Empty;

        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime CreatedAt { get; init; }
    }

    private sealed class NullableDateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && reader.TryGetDateTime(out var dateTime))
            {
                return dateTime;
            }

            return DateTime.Now;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
