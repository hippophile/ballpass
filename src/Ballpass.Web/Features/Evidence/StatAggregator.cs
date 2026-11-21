using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Evidence;

public interface IStatAggregator
{
    /// <summary>Latest season line (ordinal season order) or null.</summary>
    StatLine? LatestLine(IEnumerable<StatLine> lines);
    StatAggregate? Latest(IEnumerable<StatLine> lines);
    /// <summary>Games-weighted career aggregate.</summary>
    StatAggregate? Career(IEnumerable<StatLine> lines);
    /// <summary>Value used for percentiles: per 40 for counting stats, raw FG%.</summary>
    double MetricValue(StatLine line, EvidenceMetric metric);
    /// <summary>Number of stat lines (seasons) per source, all three sources present.</summary>
    IReadOnlyDictionary<StatSource, int> SourceMix(IEnumerable<StatLine> lines);
    TrendResult Trend(IEnumerable<StatLine> lines);
    /// <summary>Every season from the first to the last the athlete has data for; gaps have a null line.</summary>
    IReadOnlyList<SeasonSlot> Progression(IEnumerable<StatLine> lines);
}

public class StatAggregator : IStatAggregator
{
    public const int MinTrendGames = 8;
    public const decimal TrendThreshold = 0.10m;

    public StatLine? LatestLine(IEnumerable<StatLine> lines) =>
        lines.OrderByDescending(l => l.Season, StringComparer.Ordinal).FirstOrDefault();

    public StatAggregate? Latest(IEnumerable<StatLine> lines)
    {
        var line = LatestLine(lines);
        return line is null ? null : Aggregate([line]);
    }

    public StatAggregate? Career(IEnumerable<StatLine> lines)
    {
        var list = lines.ToList();
        return list.Count == 0 ? null : Aggregate(list);
    }

    public double MetricValue(StatLine line, EvidenceMetric metric) => metric switch
    {
        EvidenceMetric.Points => Per40(line.Points, line.Minutes),
        EvidenceMetric.Rebounds => Per40(line.Rebounds, line.Minutes),
        EvidenceMetric.Assists => Per40(line.Assists, line.Minutes),
        EvidenceMetric.Steals => Per40(line.Steals, line.Minutes),
        _ => (double)line.FgPct,
    };

    public IReadOnlyDictionary<StatSource, int> SourceMix(IEnumerable<StatLine> lines)
    {
        var list = lines.ToList();
        return Enum.GetValues<StatSource>().ToDictionary(s => s, s => list.Count(l => l.Source == s));
    }

    public TrendResult Trend(IEnumerable<StatLine> lines)
    {
        var list = lines.OrderByDescending(l => l.Season, StringComparer.Ordinal).ToList();
        var latest = list.FirstOrDefault();
        if (latest is null) return new(TrendDirection.NotAvailable, null, null, null);

        var previous = list.FirstOrDefault(l => SeasonYear(l.Season) == SeasonYear(latest.Season) - 1);
        if (previous is null || latest.Games < MinTrendGames || previous.Games < MinTrendGames || previous.Points <= 0)
            return new(TrendDirection.NotAvailable, null, latest.Season, previous?.Season);

        var change = (latest.Points - previous.Points) / previous.Points;
        var direction = change >= TrendThreshold ? TrendDirection.Up
            : change <= -TrendThreshold ? TrendDirection.Down
            : TrendDirection.Flat;
        return new(direction, Math.Round(change * 100, 1), latest.Season, previous.Season);
    }

    public IReadOnlyList<SeasonSlot> Progression(IEnumerable<StatLine> lines)
    {
        var bySeason = lines.GroupBy(l => SeasonYear(l.Season)).ToDictionary(g => g.Key, g => g.First());
        if (bySeason.Count == 0) return [];
        var slots = new List<SeasonSlot>();
        for (var year = bySeason.Keys.Min(); year <= bySeason.Keys.Max(); year++)
            slots.Add(new SeasonSlot(bySeason.TryGetValue(year, out var line) ? line.Season : SeasonLabel(year), line));
        return slots;
    }

    /// <summary>"2025-26" gives 2025.</summary>
    public static int SeasonYear(string season) => int.Parse(season.AsSpan(0, 4), System.Globalization.CultureInfo.InvariantCulture);

    public static string SeasonLabel(int startYear) => $"{startYear}-{(startYear + 1) % 100:00}";

    private static double Per40(decimal value, decimal minutes) => minutes <= 0 ? 0 : (double)value / (double)minutes * 40;

    // Games-weighted: per game = sum(stat*games)/sum(games); per 40 = sum(stat*games)/sum(minutes*games)*40.
    // FG% has no attempts in the data, so the career value is the games-weighted mean (an approximation).
    private static StatAggregate Aggregate(IReadOnlyList<StatLine> lines)
    {
        var games = lines.Sum(l => l.Games);
        var minutes = lines.Sum(l => (double)l.Minutes * l.Games);
        double Sum(Func<StatLine, decimal> pick) => lines.Sum(l => (double)pick(l) * l.Games);
        double PerGame(double total) => games == 0 ? 0 : total / games;
        double PerForty(double total) => minutes <= 0 ? 0 : total / minutes * 40;

        var pts = Sum(l => l.Points);
        var reb = Sum(l => l.Rebounds);
        var ast = Sum(l => l.Assists);
        var stl = Sum(l => l.Steals);
        var blk = Sum(l => l.Blocks);
        var fg = PerGame(Sum(l => l.FgPct));
        return new StatAggregate(games, minutes,
            new StatValues(PerGame(pts), PerGame(reb), PerGame(ast), PerGame(stl), PerGame(blk), fg),
            new StatValues(PerForty(pts), PerForty(reb), PerForty(ast), PerForty(stl), PerForty(blk), fg));
    }
}
