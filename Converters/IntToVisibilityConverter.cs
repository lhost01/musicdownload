using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace 网易云音乐下载
{
    /// <summary>
    /// 整数到可见性的转换器
    /// 用于根据整数索引显示/隐藏内容
    /// </summary>
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            int intValue = System.Convert.ToInt32(value);
            int intParameter = System.Convert.ToInt32(parameter);
            return intValue == intParameter ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
