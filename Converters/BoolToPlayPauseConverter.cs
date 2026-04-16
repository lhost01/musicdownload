using System;
using System.Globalization;
using System.Windows.Data;

namespace 网易云音乐下载
{
    /// <summary>
    /// 布尔值到播放/暂停文本的转换器
    /// </summary>
    public class BoolToPlayPauseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPlaying && isPlaying)
            {
                return "⏸"; // 暂停图标
            }
            return "▶"; // 播放图标
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
