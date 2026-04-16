using System;
using System.Globalization;
using System.Windows.Data;

namespace 网易云音乐下载
{
    /// <summary>
    /// 布尔值反转转换器
    /// 将 true 转换为 false，false 转换为 true
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }
    }
}
