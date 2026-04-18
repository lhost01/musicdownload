using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using 网易云音乐下载.Models;

namespace 网易云音乐下载.Services
{
    /// <summary>
    /// 网易云音乐下载服务
    /// 负责根据歌曲 ID 下载歌曲并保存为 MP3 格式
    /// </summary>
    public class NeteaseDownloadService
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 网易云音乐 API 基础地址
        /// </summary>
        /// <remarks>
        /// 注意：网易云音乐官方 API 需要加密参数和 Cookie
        /// 这里使用的是第三方 API 服务作为示例
        /// 实际开发中需要：
        /// 1. 处理加密参数（params 和 encSecKey）
        /// 2. 处理 Cookie（MUSIC_U 等）
        /// 3. 处理反爬虫机制
        /// </remarks>
        private const string API_BASE_URL = "https://music.163.com/api";

        /// <summary>
        /// 构造函数
        /// </summary>
        public NeteaseDownloadService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://music.163.com/");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// 验证歌曲 ID 是否有效
        /// </summary>
        /// <param name="songId">歌曲 ID</param>
        /// <returns>如果有效返回 true</returns>
        public bool IsValidSongId(string songId)
        {
            if (string.IsNullOrWhiteSpace(songId))
                return false;

            // 歌曲 ID 应该是纯数字
            return long.TryParse(songId, out _);
        }

        /// <summary>
        /// 获取歌曲信息
        /// </summary>
        /// <param name="songId">歌曲 ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>歌曲信息</returns>
        public async Task<SongInfo> GetSongInfoAsync(string songId, CancellationToken cancellationToken)
        {
            if (!IsValidSongId(songId))
                return null;

            try
            {
                // ============================================
                // 注意：以下是模拟获取歌曲信息
                // 实际开发中需要调用网易云音乐 API
                // ============================================
                
                // 实际 API 调用示例（需要加密参数）：
                // POST https://music.163.com/weapi/v3/song/detail
                // 参数：
                // {
                //   "c": "[{\"id\":\"歌曲ID\"}]",
                //   "csrf_token": ""
                // }
                // 需要加密：params 和 encSecKey
                
                // 模拟延迟
                await Task.Delay(500, cancellationToken);

                // 模拟返回歌曲信息
                // 实际应该从 API 响应中解析
                return new SongInfo
                {
                    Id = songId,
                    Name = string.Format("歌曲 {0}", songId),
                    Artist = "未知艺术家",
                    Album = "未知专辑",
                    Duration = 240000, // 毫秒
                    CoverUrl = string.Format("https://p1.music.126.net/song/{0}/cover.jpg", songId)
                };

                /*
                // 实际 API 调用代码示例：
                var url = string.Format("{0}/song/detail?ids={1}", API_BASE_URL, songId);
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                // 使用 Newtonsoft.Json 解析 JSON
                // 需要安装 NuGet 包：Install-Package Newtonsoft.Json
                var data = JsonConvert.DeserializeObject<dynamic>(json);
                
                if (data.code != 200)
                    throw new Exception(string.Format("API 返回错误: {0}", data.msg));
                
                var song = data.songs[0];
                return new SongInfo
                {
                    Id = song.id.ToString(),
                    Name = song.name,
                    Artist = string.Join(", ", song.ar.Select(a => a.name)),
                    Album = song.al.name,
                    Duration = song.dt,
                    CoverUrl = song.al.picUrl
                };
                */
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("获取歌曲信息失败: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// 获取歌曲下载 URL
        /// </summary>
        /// <param name="songId">歌曲 ID</param>
        /// <param name="quality">音质（standard/higher/exceed/lossless）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>下载 URL</returns>
        public async Task<string> GetDownloadUrlAsync(string songId, string quality, CancellationToken cancellationToken)
        {
            if (!IsValidSongId(songId))
                return null;

            try
            {
                // ============================================
                // 注意：以下是模拟获取下载链接
                // 实际开发中需要调用网易云音乐 API
                // ============================================
                
                // 实际 API 调用示例：
                // POST https://music.163.com/weapi/song/enhance/player/url/v1
                // 参数：
                // {
                //   "ids": "[歌曲ID]",
                //   "level": "standard",
                //   "encodeType": "mp3",
                //   "csrf_token": ""
                // }
                
                // 模拟延迟
                await Task.Delay(300, cancellationToken);

                // 模拟返回下载链接
                return string.Format("https://music.163.com/song/media/outer/url?id={0}.mp3", songId);

                /*
                // 实际 API 调用代码示例：
                var url = string.Format("{0}/song/enhance/player/url?ids={1}&br=320000", API_BASE_URL, songId);
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(json);
                
                if (data.code != 200)
                    throw new Exception(string.Format("API 返回错误: {0}", data.msg));
                
                var songData = data.data[0];
                if (songData.code != 200)
                    throw new Exception("无法获取下载链接，可能需要 VIP");
                
                return songData.url.ToString();
                */
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("获取下载链接失败: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// 下载歌曲
        /// </summary>
        /// <param name="taskInfo">下载任务信息</param>
        /// <param name="outputFolder">输出文件夹</param>
        /// <param name="progressCallback">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>下载结果</returns>
        public async Task<DownloadResult> DownloadAsync(
            DownloadTaskInfo taskInfo,
            string outputFolder,
            IProgress<DownloadProgress> progressCallback,
            CancellationToken cancellationToken)
        {
            if (taskInfo == null)
                return new DownloadResult { Success = false, ErrorMessage = "任务信息为空" };

            if (!IsValidSongId(taskInfo.SongId))
                return new DownloadResult { Success = false, ErrorMessage = "无效的歌曲 ID" };

            if (!Directory.Exists(outputFolder))
                return new DownloadResult { Success = false, ErrorMessage = "输出文件夹不存在" };

            try
            {
                // 步骤 1：获取歌曲信息
                taskInfo.Status = DownloadStatus.FetchingInfo;
                progressCallback?.Report(new DownloadProgress
                {
                    Phase = "正在获取歌曲信息...",
                    Progress = 0,
                    Status = DownloadStatus.FetchingInfo
                });

                var songInfo = await GetSongInfoAsync(taskInfo.SongId, cancellationToken);
                if (songInfo == null)
                    return new DownloadResult { Success = false, ErrorMessage = "无法获取歌曲信息" };

                taskInfo.SongName = songInfo.Name;
                taskInfo.Artist = songInfo.Artist;
                taskInfo.Album = songInfo.Album;

                // 步骤 2：获取下载链接
                progressCallback?.Report(new DownloadProgress
                {
                    Phase = "正在获取下载链接...",
                    Progress = 10,
                    Status = DownloadStatus.FetchingInfo
                });

                var downloadUrl = await GetDownloadUrlAsync(taskInfo.SongId, "standard", cancellationToken);
                if (string.IsNullOrEmpty(downloadUrl))
                    return new DownloadResult { Success = false, ErrorMessage = "无法获取下载链接，歌曲可能需要 VIP" };

                // 步骤 3：下载文件
                taskInfo.Status = DownloadStatus.Downloading;
                progressCallback?.Report(new DownloadProgress
                {
                    Phase = "正在下载...",
                    Progress = 20,
                    Status = DownloadStatus.Downloading
                });

                // 生成输出文件名
                string safeFileName = GetSafeFileName(string.Format("{0} - {1}.mp3", songInfo.Artist, songInfo.Name));
                string outputPath = Path.Combine(outputFolder, safeFileName);

                // 处理文件名冲突
                int counter = 1;
                string baseFileName = Path.GetFileNameWithoutExtension(safeFileName);
                while (File.Exists(outputPath))
                {
                    safeFileName = string.Format("{0}_{1}.mp3", baseFileName, counter);
                    outputPath = Path.Combine(outputFolder, safeFileName);
                    counter++;
                }

                // ============================================
                // 注意：以下是模拟下载过程
                // 实际开发中需要使用 HttpClient 下载文件
                // ============================================
                
                // 模拟下载过程
                int totalSteps = 10;
                for (int i = 1; i <= totalSteps; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // 模拟下载时间
                    await Task.Delay(300, cancellationToken);

                    // 更新进度（20% - 90%）
                    int progress = 20 + (i * 70) / totalSteps;
                    taskInfo.Progress = progress;
                    taskInfo.DownloadSpeed = 1024 * 1024; // 模拟 1MB/s

                    progressCallback?.Report(new DownloadProgress
                    {
                        Phase = "正在下载...",
                        Progress = progress,
                        Status = DownloadStatus.Downloading,
                        DownloadedBytes = (long)(progress * 1024 * 1024),
                        TotalBytes = 100 * 1024 * 1024,
                        Speed = taskInfo.DownloadSpeed
                    });
                }

                /*
                // 实际下载代码示例：
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        long downloadedBytes = 0;
                        int bytesRead;
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            downloadedBytes += bytesRead;
                            
                            // 计算进度和速度
                            if (totalBytes > 0)
                            {
                                int progress = (int)((downloadedBytes * 100) / totalBytes);
                                taskInfo.Progress = progress;
                                
                                double speed = downloadedBytes / stopwatch.Elapsed.TotalSeconds;
                                taskInfo.DownloadSpeed = (long)speed;
                                
                                progressCallback?.Report(new DownloadProgress
                                {
                                    Phase = "正在下载...",
                                    Progress = progress,
                                    Status = DownloadStatus.Downloading,
                                    DownloadedBytes = downloadedBytes,
                                    TotalBytes = totalBytes,
                                    Speed = (long)speed
                                });
                            }
                        }
                    }
                }
                */

                // 模拟：创建一个空文件表示下载完成
                File.WriteAllText(outputPath, "[模拟下载的音频数据]");

                // 步骤 4：完成
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
                progressCallback?.Report(new DownloadProgress
                {
                    Phase = "已取消",
                    Progress = taskInfo.Progress,
                    Status = DownloadStatus.Cancelled
                });
                return new DownloadResult { Success = false, ErrorMessage = "下载已取消", Cancelled = true };
            }
            catch (Exception ex)
            {
                taskInfo.Status = DownloadStatus.Failed;
                taskInfo.ErrorMessage = ex.Message;
                progressCallback?.Report(new DownloadProgress
                {
                    Phase = "下载失败",
                    Progress = taskInfo.Progress,
                    Status = DownloadStatus.Failed,
                    ErrorMessage = ex.Message
                });
                return new DownloadResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// 获取安全的文件名（移除非法字符）
        /// </summary>
        private string GetSafeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(fileName);
            foreach (var c in invalidChars)
            {
                sb.Replace(c, '_');
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 歌曲信息
    /// </summary>
    public class SongInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public long Duration { get; set; }
        public string CoverUrl { get; set; }
    }

    /// <summary>
    /// 下载结果
    /// </summary>
    public class DownloadResult
    {
        public bool Success { get; set; }
        public string OutputPath { get; set; }
        public string FileName { get; set; }
        public string ErrorMessage { get; set; }
        public bool Cancelled { get; set; }
        public SongInfo SongInfo { get; set; }
    }

    /// <summary>
    /// 下载进度
    /// </summary>
    public class DownloadProgress
    {
        public string Phase { get; set; }
        public int Progress { get; set; }
        public DownloadStatus Status { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public long Speed { get; set; }
        public string ErrorMessage { get; set; }
    }
}
