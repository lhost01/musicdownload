using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace 网易云音乐下载
{
    /// <summary>
    /// 布尔值到背景色的转换器
    /// </summary>
    public class BoolToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                // 选中状态的淡紫色背景
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(237, 233, 254)); // #FFEDE9FE
            }
            // 默认背景色
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 249, 249)); // #FFF9F9F9
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
