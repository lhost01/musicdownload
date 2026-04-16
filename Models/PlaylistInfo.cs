using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace 网易云音乐下载.Models
{
    /// <summary>
    /// 歌单信息
    /// </summary>
    public class PlaylistInfo : INotifyPropertyChanged
    {
        private string _name;
        private string _folderPath;
        private DateTime _createdTime;
        private ObservableCollection<PlaylistSongInfo> _songs;
        private string _coverImagePath;
        private bool _isSelected;

        /// <summary>
        /// 歌单名称
        /// </summary>
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        /// <summary>
        /// 歌单文件夹路径
        /// </summary>
        public string FolderPath
        {
            get { return _folderPath; }
            set
            {
                _folderPath = value;
                OnPropertyChanged(nameof(FolderPath));
            }
        }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime
        {
            get { return _createdTime; }
            set
            {
                _createdTime = value;
                OnPropertyChanged(nameof(CreatedTime));
            }
        }

        /// <summary>
        /// 歌曲列表
        /// </summary>
        public ObservableCollection<PlaylistSongInfo> Songs
        {
            get { return _songs; }
            set
            {
                _songs = value;
                OnPropertyChanged(nameof(Songs));
                OnPropertyChanged(nameof(SongCount));
                OnPropertyChanged(nameof(SongCountText));
            }
        }

        /// <summary>
        /// 封面图片路径
        /// </summary>
        public string CoverImagePath
        {
            get { return _coverImagePath; }
            set
            {
                _coverImagePath = value;
                OnPropertyChanged(nameof(CoverImagePath));
                OnPropertyChanged(nameof(CoverImage));
                OnPropertyChanged(nameof(HasCustomCover));
            }
        }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        /// <summary>
        /// 封面图片
        /// </summary>
        public ImageSource CoverImage
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(CoverImagePath) && File.Exists(CoverImagePath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(CoverImagePath);
                        bitmap.EndInit();
                        return bitmap;
                    }
                }
                catch { }
                return null;
            }
        }

        /// <summary>
        /// 是否有自定义封面
        /// </summary>
        public bool HasCustomCover
        {
            get { return !string.IsNullOrEmpty(CoverImagePath) && File.Exists(CoverImagePath); }
        }

        /// <summary>
        /// 歌曲数量
        /// </summary>
        public int SongCount
        {
            get { return Songs?.Count ?? 0; }
        }

        /// <summary>
        /// 歌曲数量文本
        /// </summary>
        public string SongCountText
        {
            get { return $"{SongCount} 首歌曲"; }
        }

        /// <summary>
        /// 创建时间文本
        /// </summary>
        public string CreatedTimeText
        {
            get { return CreatedTime.ToString("yyyy-MM-dd HH:mm"); }
        }

        /// <summary>
        /// 封面文件名
        /// </summary>
        public const string CoverFileName = "cover.jpg";

        public PlaylistInfo()
        {
            Songs = new ObservableCollection<PlaylistSongInfo>();
            CreatedTime = DateTime.Now;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 添加歌曲到歌单
        /// </summary>
        public void AddSong(PlaylistSongInfo song)
        {
            if (Songs.Any(s => s.FilePath == song.FilePath))
                return;

            Songs.Add(song);
            OnPropertyChanged(nameof(SongCount));
            OnPropertyChanged(nameof(SongCountText));
        }

        /// <summary>
        /// 从歌单移除歌曲
        /// </summary>
        public void RemoveSong(PlaylistSongInfo song)
        {
            Songs.Remove(song);
            OnPropertyChanged(nameof(SongCount));
            OnPropertyChanged(nameof(SongCountText));
        }

        /// <summary>
        /// 重命名歌单（同时重命名文件夹）
        /// </summary>
        public bool Rename(string newName, string parentDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newName) || newName == Name)
                    return false;

                string newFolderPath = Path.Combine(parentDirectory, newName);
                if (Directory.Exists(newFolderPath))
                    return false;

                Directory.Move(FolderPath, newFolderPath);
                Name = newName;
                FolderPath = newFolderPath;

                // 更新所有歌曲的文件路径
                foreach (var song in Songs)
                {
                    song.UpdateFolderPath(newFolderPath);
                }

                // 更新封面路径
                if (HasCustomCover)
                {
                    CoverImagePath = Path.Combine(newFolderPath, CoverFileName);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 设置封面图片
        /// </summary>
        public bool SetCoverImage(string sourceImagePath)
        {
            try
            {
                if (!File.Exists(sourceImagePath))
                    return false;

                string destPath = Path.Combine(FolderPath, CoverFileName);
                File.Copy(sourceImagePath, destPath, true);
                CoverImagePath = destPath;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 删除封面图片
        /// </summary>
        public bool RemoveCoverImage()
        {
            try
            {
                if (HasCustomCover)
                {
                    File.Delete(CoverImagePath);
                    CoverImagePath = null;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查并加载封面
        /// </summary>
        public void CheckAndLoadCover()
        {
            string coverPath = Path.Combine(FolderPath, CoverFileName);
            if (File.Exists(coverPath))
            {
                CoverImagePath = coverPath;
            }
        }
    }
}
