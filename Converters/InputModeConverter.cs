using System;
using System.Globalization;
using System.Windows.Data;

namespace 网易云音乐下载
{
    /// <summary>
    /// 输入模式转换器
    /// 用于 RadioButton 绑定到 InputMode 枚举
    /// </summary>
    public class InputModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string currentModeStr = value.ToString();
            string targetModeStr = parameter.ToString();
            
            return string.Equals(currentModeStr, targetModeStr, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return parameter.ToString();
            }
            return Binding.DoNothing;
        }
    }
}
