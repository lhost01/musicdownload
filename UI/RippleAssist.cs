using System.Windows;
using System.Windows.Media;

namespace MusicDownload.UI
{
    public static class RippleAssist
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.RegisterAttached(
                "CornerRadius",
                typeof(CornerRadius),
                typeof(RippleAssist),
                new FrameworkPropertyMetadata(new CornerRadius(14), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RippleBrushProperty =
            DependencyProperty.RegisterAttached(
                "RippleBrush",
                typeof(Brush),
                typeof(RippleAssist),
                new FrameworkPropertyMetadata(
                    new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShadowColorProperty =
            DependencyProperty.RegisterAttached(
                "ShadowColor",
                typeof(Color),
                typeof(RippleAssist),
                new FrameworkPropertyMetadata(Color.FromRgb(79, 139, 255), FrameworkPropertyMetadataOptions.AffectsRender));

        public static CornerRadius GetCornerRadius(DependencyObject obj)
        {
            return (CornerRadius)obj.GetValue(CornerRadiusProperty);
        }

        public static void SetCornerRadius(DependencyObject obj, CornerRadius value)
        {
            obj.SetValue(CornerRadiusProperty, value);
        }

        public static Brush GetRippleBrush(DependencyObject obj)
        {
            return (Brush)obj.GetValue(RippleBrushProperty);
        }

        public static void SetRippleBrush(DependencyObject obj, Brush value)
        {
            obj.SetValue(RippleBrushProperty, value);
        }

        public static Color GetShadowColor(DependencyObject obj)
        {
            return (Color)obj.GetValue(ShadowColorProperty);
        }

        public static void SetShadowColor(DependencyObject obj, Color value)
        {
            obj.SetValue(ShadowColorProperty, value);
        }
    }
}
