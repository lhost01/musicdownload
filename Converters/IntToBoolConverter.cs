using System;
using System.Globalization;
using System.Windows.Data;

namespace 网易云音乐下载
{
    /// <summary>
    /// 整数到布尔值的转换器
    /// 用于将整数绑定到 RadioButton 的 IsChecked 属性
    /// </summary>
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            int intValue = System.Convert.ToInt32(value);
            int intParameter = System.Convert.ToInt32(parameter);
            return intValue == intParameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return System.Convert.ToInt32(parameter);
            }
            return Binding.DoNothing;
        }
    }
}
