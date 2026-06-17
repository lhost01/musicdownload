using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Musicbox.Controls;

public sealed class AmbientWaveControl : Control
{
    private readonly DispatcherTimer _timer;
    private double _time;

    public AmbientWaveControl()
    {
        ClipToBounds = true;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _timer.Tick += (_, _) =>
        {
            _time += 0.033;
            InvalidateVisual();
        };

        AttachedToVisualTree += (_, _) => _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse("#131B3A"), 0),
                new GradientStop(Color.Parse("#0D1530"), 0.55),
                new GradientStop(Color.Parse("#090F24"), 1)
            ]
        };

        context.FillRectangle(background, bounds);

        DrawOrb(context, bounds, 0.18, 0.28, 110, Color.Parse("#6F8CFF"), 0.9);
        DrawOrb(context, bounds, 0.72, 0.34, 86, Color.Parse("#7BFFD8"), 1.3);
        DrawOrb(context, bounds, 0.54, 0.70, 140, Color.Parse("#9D6CFF"), 1.0);

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1.2);
        var waveGeometry = new StreamGeometry();
        using (var geometry = waveGeometry.Open())
        {
            var startY = bounds.Height * 0.62;
            geometry.BeginFigure(new Point(0, startY), false);
            for (var x = 0; x <= bounds.Width; x += 8)
            {
                var y = startY +
                        Math.Sin(x / 54d + _time * 1.8) * 10 +
                        Math.Cos(x / 34d + _time * 1.2) * 8;
                geometry.LineTo(new Point(x, y));
            }
        }

        context.DrawGeometry(null, pen, waveGeometry);

        var barCount = 18;
        var barWidth = Math.Max(8, bounds.Width / 42);
        for (var index = 0; index < barCount; index++)
        {
            var amplitude = 0.25 + (Math.Sin(_time * 2.6 + index * 0.55) + 1) * 0.35;
            var height = amplitude * bounds.Height * 0.28;
            var x = bounds.Width * 0.15 + index * (barWidth + 5);
            var rect = new Rect(x, bounds.Height - 30 - height, barWidth, height);
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(Color.Parse("#2CF1E3"), 0),
                    new GradientStop(Color.Parse("#7D72FF"), 1)
                ]
            };
            context.FillRectangle(brush, rect, 6);
        }
    }

    private void DrawOrb(
        DrawingContext context,
        Rect bounds,
        double relativeX,
        double relativeY,
        double baseRadius,
        Color color,
        double speed)
    {
        var x = bounds.Width * relativeX + Math.Cos(_time * speed) * 24;
        var y = bounds.Height * relativeY + Math.Sin(_time * speed * 1.2) * 20;
        var radius = baseRadius + Math.Sin(_time * speed * 1.4) * 18;

        var brush = new SolidColorBrush(Color.FromArgb(65, color.R, color.G, color.B));
        context.DrawEllipse(brush, null, new Point(x, y), radius, radius);
    }
}
