using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace BankingThreads.UI.Controls;

// Draws observed worker-completion snapshots, not invented transactions or smoothed data.
public sealed class BalanceChart : Control
{
    private readonly List<(double Milliseconds, decimal Balance)> _samples = new();
    public decimal? Reference { get; private set; }
    public IReadOnlyList<(double Milliseconds, decimal Balance)> Samples => _samples;
    private static readonly IBrush Blue = SolidColorBrush.Parse("#3478F6");
    private static readonly IBrush Muted = SolidColorBrush.Parse("#9A9FAB");

    public void Reset(decimal starting)
    {
        _samples.Clear();
        _samples.Add((0, starting));
        Reference = null;
        InvalidateVisual();
    }

    public void AddSample(double milliseconds, decimal balance)
    {
        _samples.Add((milliseconds, balance));
        InvalidateVisual();
    }

    public void Complete(decimal reference, double duration, decimal balance)
    {
        Reference = reference;
        AddSample(duration, balance);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_samples.Count == 0 || Bounds.Width < 100 || Bounds.Height < 80) return;
        double left = 8, top = 16, right = Bounds.Width - 58, bottom = Bounds.Height - 28;
        var values = _samples.Select(x => (double)x.Balance).ToList();
        if (Reference is decimal reference) values.Add((double)reference);
        double minimum = values.Min(), maximum = values.Max();
        double padding = Math.Max((maximum - minimum) * 0.18, 50);
        minimum -= padding; maximum += padding;
        double last = Math.Max(_samples.Max(x => x.Milliseconds), 1);
        double Y(double v) => top + (maximum - v) / (maximum - minimum) * (bottom - top);
        Point Position((double Milliseconds, decimal Balance) p) =>
            new(left + p.Milliseconds / last * (right - left), Y((double)p.Balance));

        for (int i = 0; i < 4; i++)
        {
            double value = minimum + (maximum - minimum) * i / 3;
            double y = Y(value);
            context.DrawLine(new Pen(SolidColorBrush.Parse("#EEF0F5"), 1), new Point(left, y), new Point(right, y));
            context.DrawText(Text(value.ToString("N0", CultureInfo.InvariantCulture), Muted), new Point(right + 10, y - 6));
        }
        if (Reference is decimal baseline)
            context.DrawLine(new Pen(SolidColorBrush.Parse("#B7BDCA"), 1, new DashStyle(new[] { 4d, 4d }, 0)),
                new Point(left, Y((double)baseline)), new Point(right, Y((double)baseline)));

        if (_samples.Count > 1)
        {
            var fill = new StreamGeometry();
            using (var path = fill.Open())
            {
                path.BeginFigure(new Point(left, bottom), true);
                foreach (var point in _samples) path.LineTo(Position(point));
                path.LineTo(new Point(Position(_samples[^1]).X, bottom));
                path.EndFigure(true);
            }
            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.Parse("#303478F6"), 0),
                    new GradientStop(Color.Parse("#023478F6"), 1)
                }
            };
            context.DrawGeometry(gradient, null, fill);
            var line = new StreamGeometry();
            using (var path = line.Open())
            {
                path.BeginFigure(Position(_samples[0]), false);
                foreach (var sample in _samples.Skip(1)) path.LineTo(Position(sample));
                path.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(Blue, 2.5), line);
            Point endpoint = Position(_samples[^1]);
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#203478F6")), null, endpoint, 9, 9);
            context.DrawEllipse(Brushes.White, new Pen(Blue, 2), endpoint, 4, 4);
        }
        else
        {
            double y = Y((double)_samples[0].Balance);
            context.DrawLine(new Pen(SolidColorBrush.Parse("#BBCFF5"), 1.5,
                new DashStyle(new[] { 5d, 5d }, 0)), new Point(left, y), new Point(right, y));
            var caption = Text("Your next experiment starts here", Muted, 12);
            context.DrawText(caption, new Point(left + (right - left - caption.Width) / 2, y + 16));
        }
        context.DrawText(Text("START", Muted, 9), new Point(left, bottom + 13));
        var end = Text(_samples.Count > 1 ? $"{last:0} ms" : "WORKER SNAPSHOTS", Muted, 9);
        context.DrawText(end, new Point(right - end.Width, bottom + 13));
    }

    private static FormattedText Text(string value, IBrush color, double size = 10) =>
        new(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("fonts:Inter#Inter"), size, color);
}
