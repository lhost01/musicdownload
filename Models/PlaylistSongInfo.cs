using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;

namespace 网易云音乐下载.Models
{
    /// <summary>
    /// 歌单中的歌曲信息
    /// </summary>
    public class PlaylistSongInfo : INotifyPropertyChanged
    {
        private string _fileName;
        private string _filePath;
        private string _title;
        private string _artist;
        private string _album;
        private long _fileSize;
        private TimeSpan _duration;
        private DateTime _addedTime;

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName
        {
            get { return _fileName; }
            set
            {
                _fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath
        {
            get { return _filePath; }
            set
            {
                _filePath = value;
                OnPropertyChanged(nameof(FilePath));
            }
        }

        /// <summary>
        /// 歌曲标题
        /// </summary>
        public string Title
        {
            get { return _title; }
            set
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        /// <summary>
        /// 艺术家
        /// </summary>
        public string Artist
        {
            get { return _artist; }
            set
            {
                _artist = value;
                OnPropertyChanged(nameof(Artist));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(ArtistText));
            }
        }

        /// <summary>
        /// 专辑
        /// </summary>
        public string Album
        {
            get { return _album; }
            set
            {
                _album = value;
                OnPropertyChanged(nameof(Album));
                OnPropertyChanged(nameof(ArtistText));
            }
        }

        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize
        {
            get { return _fileSize; }
            set
            {
                _fileSize = value;
                OnPropertyChanged(nameof(FileSize));
                OnPropertyChanged(nameof(FileSizeText));
            }
        }

        /// <summary>
        /// 时长
        /// </summary>
        public TimeSpan Duration
        {
            get { return _duration; }
            set
            {
                _duration = value;
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(DurationText));
            }
        }

        /// <summary>
        /// 添加时间
        /// </summary>
        public DateTime AddedTime
        {
            get { return _addedTime; }
            set
            {
                _addedTime = value;
                OnPropertyChanged(nameof(AddedTime));
                OnPropertyChanged(nameof(AddedTimeText));
            }
        }

        /// <summary>
        /// 显示名称（优先使用标题，否则使用文件名）
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                {
                    return string.IsNullOrWhiteSpace(Artist) ? Title : $"{Title} - {Artist}";
                }
                return FileName;
            }
        }

        /// <summary>
        /// 艺术家文本
        /// </summary>
        public string ArtistText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Album))
                    return $"{Artist} · {Album}";
                return Artist ?? Album ?? "";
            }
        }

        /// <summary>
        /// 文件大小文本
        /// </summary>
        public string FileSizeText
        {
            get
            {
                if (FileSize < 1024)
                    return $"{FileSize} B";
                if (FileSize < 1024 * 1024)
                    return $"{FileSize / 1024.0:F1} KB";
                return $"{FileSize / (1024.0 * 1024.0):F1} MB";
            }
        }

        /// <summary>
        /// 时长文本
        /// </summary>
        public string DurationText
        {
            get
            {
                if (Duration.TotalHours >= 1)
                    return Duration.ToString("h\\:mm\\:ss");
                return Duration.ToString("m\\:ss");
            }
        }

        /// <summary>
        /// 添加时间文本
        /// </summary>
        public string AddedTimeText
        {
            get { return AddedTime.ToString("yyyy-MM-dd"); }
        }

        public PlaylistSongInfo()
        {
            AddedTime = DateTime.Now;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 从NcmFileInfo创建
        /// </summary>
        public static PlaylistSongInfo FromNcmFile(NcmFileInfo ncmFile)
        {
            return new PlaylistSongInfo
            {
                FileName = ncmFile.FileName,
                FilePath = ncmFile.FullPath,
                FileSize = ncmFile.FileSize
            };
        }

        /// <summary>
        /// 从文件路径创建
        /// </summary>
        public static PlaylistSongInfo FromFilePath(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                return null;

            var songInfo = new PlaylistSongInfo
            {
                FileName = fileInfo.Name,
                FilePath = fileInfo.FullName,
                FileSize = fileInfo.Length
            };

            // 尝试读取音频文件元数据
            try
            {
                var mediaPlayer = new MediaPlayer();
                mediaPlayer.Open(new Uri(filePath));
                
                // 等待媒体打开完成（最多等待 500ms）
                int waitCount = 0;
                while (mediaPlayer.NaturalDuration.TimeSpan == TimeSpan.Zero && waitCount < 25)
                {
                    System.Threading.Thread.Sleep(20);
                    waitCount++;
                }
                
                if (mediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    songInfo.Duration = mediaPlayer.NaturalDuration.TimeSpan;
                }
                
                mediaPlayer.Close();
            }
            catch { }

            return songInfo;
        }

        /// <summary>
        /// 更新文件夹路径（当歌单重命名时调用）
        /// </summary>
        public void UpdateFolderPath(string newFolderPath)
        {
            FilePath = Path.Combine(newFolderPath, FileName);
        }
    }
}
