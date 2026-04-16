using System;

namespace 网易云音乐下载.Models
{
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

    public class DownloadTaskInfo
    {
        public Guid Id { get; set; }
        public string SongId { get; set; }
        public string SongName { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public DownloadStatus Status { get; set; }
        public int Progress { get; set; }
        public string OutputPath { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public long FileSize { get; set; }
        public long DownloadSpeed { get; set; }

        public string FileSizeText
        {
            get { return FormatFileSize(FileSize); }
        }

        public string DownloadSpeedText
        {
            get { return DownloadSpeed > 0 ? FormatFileSize(DownloadSpeed) + "/s" : ""; }
        }

        public string StatusText
        {
            get { return GetStatusText(Status); }
        }

        public DownloadTaskInfo()
        {
            Id = Guid.NewGuid();
            CreatedTime = DateTime.Now;
            Status = DownloadStatus.Pending;
            Progress = 0;
        }

        private string FormatFileSize(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB)
                return string.Format("{0:F2} GB", bytes / (double)GB);
            if (bytes >= MB)
                return string.Format("{0:F2} MB", bytes / (double)MB);
            if (bytes >= KB)
                return string.Format("{0:F2} KB", bytes / (double)KB);
            return string.Format("{0} B", bytes);
        }

        private string GetStatusText(DownloadStatus status)
        {
            switch (status)
            {
                case DownloadStatus.Pending:
                    return "等待中";
                case DownloadStatus.FetchingInfo:
                    return "获取信息中";
                case DownloadStatus.Downloading:
                    return "下载中";
                case DownloadStatus.Converting:
                    return "转换中";
                case DownloadStatus.Completed:
                    return "已完成";
                case DownloadStatus.Failed:
                    return "失败";
                case DownloadStatus.Cancelled:
                    return "已取消";
                default:
                    return "未知";
            }
        }
    }
}
