using System.Buffers;
using System.Net;
using System.Text;
using System.Text.Json;
using Musicbox.Models;

namespace Musicbox.Services;

public sealed class NeteaseDownloadService
{
    private const string ByfunsApiBase = "https://api.byfuns.top/1/";

    private static readonly string[] Mp3QualityLevels = ["exhigh", "higher", "standard"];

    private static readonly string[] FlacQualityLevels = ["lossless", "hire"];

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    public NeteaseDownloadService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://music.163.com/");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
    }

    public bool IsValidSongId(string songId)
    {
        return !string.IsNullOrWhiteSpace(songId) && long.TryParse(songId, out _);
    }

    public async Task<IReadOnlyList<SongSearchResult>> SearchSongsAsync(string keyword, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        try
        {
            var encodedKeyword = Uri.EscapeDataString(keyword.Trim());
            var url = $"https://music.163.com/api/search/get/web?csrf_token=hlpretag=&hlposttag=&s={encodedKeyword}&type=1&offset=0&total=true&limit={Math.Clamp(limit, 1, 50)}";
            var doc = await GetJsonDocumentAsync(url, cancellationToken);
            if (doc is null)
            {
                return [];
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (!IsSuccessResponse(root))
                {
                    return [];
                }

                if (!root.TryGetProperty("result", out var result) ||
                    !result.TryGetProperty("songs", out var songs))
                {
                    return [];
                }

                return ParseSearchResults(songs);
            }
        }
        catch (TaskCanceledException)
        {
            return [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<SongInfo?> GetSongInfoAsync(string songId, CancellationToken cancellationToken)
    {
        if (!IsValidSongId(songId))
        {
            return null;
        }

        try
        {
            var url = $"https://music.163.com/api/song/detail?ids=%5B{songId}%5D";
            var doc = await GetJsonDocumentAsync(url, cancellationToken);
            if (doc is null)
            {
                return null;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (!IsSuccessResponse(root))
                {
                    return null;
                }

                if (!root.TryGetProperty("songs", out var songs) || songs.GetArrayLength() == 0)
                {
                    return null;
                }

                return ParseSongInfo(songs[0], songId);
            }
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<SongUrlInfo?> GetDownloadUrlAsync(
        string songId,
        DownloadAudioFormat format,
        CancellationToken cancellationToken)
    {
        if (!IsValidSongId(songId))
        {
            return null;
        }

        if (format == DownloadAudioFormat.Mp3)
        {
            return await TryGetUrlForLevelsAsync(songId, Mp3QualityLevels, requireFlac: false, cancellationToken);
        }

        var flacUrl = await TryGetUrlForLevelsAsync(songId, FlacQualityLevels, requireFlac: true, cancellationToken);
        if (flacUrl is not null)
        {
            return flacUrl;
        }

        return await TryGetUrlForLevelsAsync(songId, Mp3QualityLevels, requireFlac: false, cancellationToken);
    }

    private async Task<SongUrlInfo?> TryGetUrlForLevelsAsync(
        string songId,
        IReadOnlyList<string> levels,
        bool requireFlac,
        CancellationToken cancellationToken)
    {
        foreach (var level in levels)
        {
            var urlInfo = await TryGetUrlFromByfunsAsync(songId, level, cancellationToken);
            if (urlInfo is null)
            {
                continue;
            }

            var isFlac = IsFlacUrl(urlInfo.Url);
            if (requireFlac && !isFlac)
            {
                continue;
            }

            if (!requireFlac && isFlac)
            {
                continue;
            }

            return urlInfo;
        }

        return null;
    }

    public async Task<DownloadResult> DownloadAsync(
        DownloadTaskInfo taskInfo,
        string outputFolder,
        DownloadAudioFormat format,
        IProgress<DownloadProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        if (!IsValidSongId(taskInfo.SongId))
        {
            return new DownloadResult { Success = false, ErrorMessage = "无效的歌曲 ID" };
        }

        if (!Directory.Exists(outputFolder))
        {
            return new DownloadResult { Success = false, ErrorMessage = "输出文件夹不存在" };
        }

        try
        {
            taskInfo.Status = DownloadStatus.FetchingInfo;
            progressCallback?.Report(new DownloadProgress
            {
                Phase = "正在获取歌曲信息...",
                Progress = 0,
                Status = DownloadStatus.FetchingInfo
            });

            var songInfo = await GetSongInfoAsync(taskInfo.SongId, cancellationToken);
            if (songInfo is null)
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = "无法获取歌曲信息，请确认歌曲 ID 是否正确"
                };
            }

            taskInfo.SongName = songInfo.Name;
            taskInfo.Artist = songInfo.Artist;
            taskInfo.Album = songInfo.Album;

            progressCallback?.Report(new DownloadProgress
            {
                Phase = "正在获取下载链接...",
                Progress = 10,
                Status = DownloadStatus.FetchingInfo
            });

            var urlInfo = await GetDownloadUrlAsync(taskInfo.SongId, format, cancellationToken);
            if (urlInfo is null || string.IsNullOrWhiteSpace(urlInfo.Url))
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = "无法获取下载链接，该歌曲可能不存在或因版权限制无法下载"
                };
            }

            taskInfo.Status = DownloadStatus.Downloading;

            var extension = ResolveExtension(urlInfo);
            var safeFileName = GetSafeFileName($"{songInfo.Artist} - {songInfo.Name}{extension}");
            var outputPath = Path.Combine(outputFolder, safeFileName);
            var counter = 1;
            var baseName = Path.GetFileNameWithoutExtension(safeFileName);

            while (File.Exists(outputPath))
            {
                safeFileName = $"{baseName}_{counter}{extension}";
                outputPath = Path.Combine(outputFolder, safeFileName);
                counter++;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, urlInfo.Url);
            request.Headers.Referrer = new Uri("https://music.163.com/");

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new DownloadResult { Success = false, ErrorMessage = $"下载失败: HTTP {(int)response.StatusCode}" };
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);

            var buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                long downloadedBytes = 0;
                int bytesRead;
                var lastReportTime = DateTime.UtcNow;
                long lastReportBytes = 0;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    downloadedBytes += bytesRead;

                    var now = DateTime.UtcNow;
                    var elapsed = (now - lastReportTime).TotalMilliseconds;
                    if (elapsed >= 200 || downloadedBytes == totalBytes)
                    {
                        var speed = elapsed > 0
                            ? (long)((downloadedBytes - lastReportBytes) / elapsed * 1000)
                            : 0;

                        var progress = totalBytes > 0
                            ? (int)(downloadedBytes * 100 / totalBytes)
                            : 50;

                        taskInfo.Progress = progress;
                        taskInfo.DownloadSpeed = speed;

                        progressCallback?.Report(new DownloadProgress
                        {
                            Phase = "正在下载...",
                            Progress = progress,
                            Status = DownloadStatus.Downloading,
                            DownloadedBytes = downloadedBytes,
                            TotalBytes = totalBytes,
                            Speed = speed
                        });

                        lastReportTime = now;
                        lastReportBytes = downloadedBytes;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            taskInfo.Status = DownloadStatus.Completed;
            taskInfo.Progress = 100;
            taskInfo.OutputPath = outputPath;
            taskInfo.CompletedTime = DateTime.Now;
            taskInfo.FileSize = new FileInfo(outputPath).Length;

            progressCallback?.Report(new DownloadProgress
            {
                Phase = "下载完成",
                Progress = 100,
                Status = DownloadStatus.Completed
            });

            return new DownloadResult
            {
                Success = true,
                OutputPath = outputPath,
                FileName = safeFileName,
                SongInfo = songInfo
            };
        }
        catch (OperationCanceledException)
        {
            taskInfo.Status = DownloadStatus.Cancelled;
            return new DownloadResult { Success = false, ErrorMessage = "下载已取消", Cancelled = true };
        }
        catch (HttpRequestException ex)
        {
            taskInfo.Status = DownloadStatus.Failed;
            taskInfo.ErrorMessage = ex.Message;
            return new DownloadResult { Success = false, ErrorMessage = $"网络错误: {ex.Message}" };
        }
        catch (IOException ex)
        {
            taskInfo.Status = DownloadStatus.Failed;
            taskInfo.ErrorMessage = ex.Message;
            return new DownloadResult { Success = false, ErrorMessage = $"文件写入错误: {ex.Message}" };
        }
        catch (Exception ex)
        {
            taskInfo.Status = DownloadStatus.Failed;
            taskInfo.ErrorMessage = ex.Message;
            return new DownloadResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<SongUrlInfo?> TryGetUrlFromByfunsAsync(string songId, string level, CancellationToken cancellationToken)
    {
        try
        {
            var requestUrl = $"{ByfunsApiBase}?id={songId}&level={level}";
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            if (string.IsNullOrWhiteSpace(body) ||
                body.StartsWith("404", StringComparison.OrdinalIgnoreCase) ||
                !Uri.TryCreate(body, UriKind.Absolute, out _))
            {
                return null;
            }

            return new SongUrlInfo
            {
                Url = body,
                Type = InferTypeFromUrl(body, level)
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool IsFlacUrl(string url)
    {
        return url.Contains(".flac", StringComparison.OrdinalIgnoreCase);
    }

    private static string? InferTypeFromUrl(string url, string level)
    {
        if (url.Contains(".flac", StringComparison.OrdinalIgnoreCase) || level == "lossless" || level == "hire")
        {
            return "flac";
        }

        if (url.Contains(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return "mp3";
        }

        return level == "lossless" ? "flac" : "mp3";
    }

    private async Task<JsonDocument?> GetJsonDocumentAsync(string url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json);
    }

    private static bool IsSuccessResponse(JsonElement root)
    {
        return root.TryGetProperty("code", out var code) && code.GetInt32() == 200;
    }

    private static List<SongSearchResult> ParseSearchResults(JsonElement songs)
    {
        var items = new List<SongSearchResult>();
        foreach (var song in songs.EnumerateArray())
        {
            var songId = song.TryGetProperty("id", out var idProp) ? idProp.GetInt64().ToString() : string.Empty;
            if (string.IsNullOrWhiteSpace(songId))
            {
                continue;
            }

            var songName = song.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString() ?? "未知歌曲"
                : "未知歌曲";

            var duration = song.TryGetProperty("dt", out var durationProp) ? durationProp.GetInt64() : 0;

            var artistNames = new List<string>();
            if (song.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array)
            {
                foreach (var artist in artists.EnumerateArray())
                {
                    if (artist.TryGetProperty("name", out var artistName))
                    {
                        var text = artistName.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            artistNames.Add(text);
                        }
                    }
                }
            }

            var album = string.Empty;
            if (song.TryGetProperty("album", out var albumProp) &&
                albumProp.TryGetProperty("name", out var albumNameProp))
            {
                album = albumNameProp.GetString() ?? string.Empty;
            }

            items.Add(new SongSearchResult
            {
                SongId = songId,
                SongName = songName,
                Artist = artistNames.Count > 0 ? string.Join("/", artistNames) : "未知艺术家",
                Album = album,
                Duration = duration
            });
        }

        return items;
    }

    private static SongInfo ParseSongInfo(JsonElement song, string songId)
    {
        var name = song.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "未知歌曲" : "未知歌曲";
        var duration = song.TryGetProperty("dt", out var dtProp) ? dtProp.GetInt64() : 0;

        var artist = "未知艺术家";
        if (song.TryGetProperty("ar", out var ar) && ar.GetArrayLength() > 0)
        {
            var artistNames = new List<string>();
            foreach (var artistObj in ar.EnumerateArray())
            {
                if (artistObj.TryGetProperty("name", out var artistName))
                {
                    artistNames.Add(artistName.GetString() ?? string.Empty);
                }
            }

            if (artistNames.Count > 0)
            {
                artist = string.Join("/", artistNames);
            }
        }

        var album = "未知专辑";
        var coverUrl = string.Empty;
        if (song.TryGetProperty("al", out var al))
        {
            if (al.TryGetProperty("name", out var albumName))
            {
                album = albumName.GetString() ?? "未知专辑";
            }

            if (al.TryGetProperty("picUrl", out var picUrl))
            {
                coverUrl = picUrl.GetString() ?? string.Empty;
            }
        }

        return new SongInfo
        {
            Id = songId,
            Name = name,
            Artist = artist,
            Album = album,
            Duration = duration,
            CoverUrl = coverUrl
        };
    }

    private static string ResolveExtension(SongUrlInfo urlInfo)
    {
        if (!string.IsNullOrWhiteSpace(urlInfo.Type))
        {
            return urlInfo.Type.StartsWith('.') ? urlInfo.Type : $".{urlInfo.Type}";
        }

        if (urlInfo.Url.Contains(".flac", StringComparison.OrdinalIgnoreCase))
        {
            return ".flac";
        }

        return ".mp3";
    }

    private static string GetSafeFileName(string fileName)
    {
        var builder = new StringBuilder(fileName);
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            builder.Replace(invalidChar, '_');
        }

        return builder.ToString();
    }
}

public sealed class SongUrlInfo
{
    public string Url { get; init; } = string.Empty;

    public string? Type { get; init; }
}

public sealed class SongInfo
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public string Album { get; init; } = string.Empty;

    public long Duration { get; init; }

    public string CoverUrl { get; init; } = string.Empty;
}

public sealed class DownloadResult
{
    public bool Success { get; init; }

    public string OutputPath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public bool Cancelled { get; init; }

    public SongInfo? SongInfo { get; init; }
}

public sealed class DownloadProgress
{
    public string Phase { get; init; } = string.Empty;

    public int Progress { get; init; }

    public DownloadStatus Status { get; init; }

    public long DownloadedBytes { get; init; }

    public long TotalBytes { get; init; }

    public long Speed { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
