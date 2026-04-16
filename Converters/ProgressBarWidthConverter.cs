using System;
using System.Globalization;
using System.Windows.Data;

namespace 网易云音乐下载
{
    /// <summary>
    /// 进度条宽度转换器
    /// 根据当前值、最大值和轨道宽度计算进度指示器的宽度
    /// </summary>
    public class ProgressBarWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3 &&
                values[0] is double currentValue &&
                values[1] is double maximum &&
                values[2] is double trackWidth)
            {
                if (maximum <= 0)
                    return 0.0;

                double percentage = currentValue / maximum;
                return trackWidth * percentage;
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
