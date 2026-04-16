using System;
using System.Globalization;
using System.Windows.Data;

namespace 网易云音乐下载
{
    /// <summary>
    /// TimeSpan 到秒数的转换器
    /// </summary>
    public class TimeSpanToSecondsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeSpan)
            {
                return timeSpan.TotalSeconds;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double seconds)
            {
                return TimeSpan.FromSeconds(seconds);
            }
            return TimeSpan.Zero;
        }
    }
}
