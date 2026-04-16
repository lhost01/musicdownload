using System;

namespace 网易云音乐下载.Models
{
    public enum ConversionStatus
    {
        Pending,
        Converting,
        Completed,
        Failed,
        Cancelled
    }

    public class NcmFileInfo
    {
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public long FileSize { get; set; }
        public ConversionStatus Status { get; set; }
        public int Progress { get; set; }
        public string OutputPath { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime AddedTime { get; set; }

        public string FileSizeText
        {
            get { return FormatFileSize(FileSize); }
        }

        public string StatusText
        {
            get { return GetStatusText(Status); }
        }

        public NcmFileInfo()
        {
            Id = Guid.NewGuid();
            AddedTime = DateTime.Now;
            Status = ConversionStatus.Pending;
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

        private string GetStatusText(ConversionStatus status)
        {
            switch (status)
            {
                case ConversionStatus.Pending:
                    return "等待中";
                case ConversionStatus.Converting:
                    return "转换中";
                case ConversionStatus.Completed:
                    return "已完成";
                case ConversionStatus.Failed:
                    return "失败";
                case ConversionStatus.Cancelled:
                    return "已取消";
                default:
                    return "未知";
            }
        }
    }
}
