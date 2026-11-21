using System.Globalization;

namespace Ballpass.Web.Features.Evidence;

public record ChartPoint(string Season, double X, double Y, decimal Value);

/// <param name="Segments">Consecutive seasons with data; a missing season starts a new segment (line break).</param>
public record ProgressionLayout(double Width, double Height, IReadOnlyList<IReadOnlyList<ChartPoint>> Segments,
    IReadOnlyList<(string Season, double X)> Axis)
{
    public double BaselineY { get; init; }
}

/// <summary>SVG numbers. Every number that ends up in markup goes through <see cref="Fmt"/> (invariant culture).</summary>
public static class ChartGeometry
{
    public const double TrackWidth = 200;

    private const double PadLeft = 22, PadRight = 22, PadTop = 26, PadBottom = 30;

    public static string Fmt(double value) =>
        Math.Abs(value) < 0.005 ? "0" : value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Percentile 78 on a 200 unit track is 156 units wide.</summary>
    public static double FillWidth(int percentile, double trackWidth = TrackWidth) => percentile / 100.0 * trackWidth;

    /// <summary>SVG points attribute: "x,y x,y" with dots as decimal separator.</summary>
    public static string PointsAttr(IEnumerable<ChartPoint> points) =>
        string.Join(' ', points.Select(p => $"{Fmt(p.X)},{Fmt(p.Y)}"));

    public static ProgressionLayout Layout(IReadOnlyList<SeasonSlot> slots, double width, double height)
    {
        var plotW = width - PadLeft - PadRight;
        var plotH = height - PadTop - PadBottom;
        var max = slots.Where(s => s.Line is not null).Select(s => (double)s.Line!.Points).DefaultIfEmpty(1).Max();
        if (max <= 0) max = 1;

        double X(int i) => slots.Count == 1 ? PadLeft + plotW / 2 : PadLeft + i * plotW / (slots.Count - 1);
        double Y(double v) => PadTop + plotH * (1 - v / max);

        var segments = new List<IReadOnlyList<ChartPoint>>();
        List<ChartPoint>? current = null;
        for (var i = 0; i < slots.Count; i++)
        {
            var line = slots[i].Line;
            if (line is null) { current = null; continue; }
            if (current is null) { current = []; segments.Add(current); }
            current.Add(new ChartPoint(slots[i].Season, X(i), Y((double)line.Points), line.Points));
        }
        var axis = slots.Select((s, i) => (s.Season, X(i))).ToList();
        return new ProgressionLayout(width, height, segments, axis) { BaselineY = PadTop + plotH };
    }
}
