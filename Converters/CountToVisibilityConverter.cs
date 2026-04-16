using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace 网易云音乐下载
{
    /// <summary>
    /// 计数到可见性的转换器
    /// 当计数大于 0 时返回 Visible，否则返回 Collapsed
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
