using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Evidence;

public interface IBadgeService
{
    /// <param name="pointsPercentile">Valid points-per-40 percentile in the demo cohort, or null when not computable.</param>
    IReadOnlyList<Badge> Evaluate(IEnumerable<StatLine> lines, int? pointsPercentile);
}

/// <summary>
/// Six rule badges. Each carries a rule code (resource key Badge_{Rule}) and the numbers its text prints,
/// so the badge always names the rule that produced it. Thresholds are our estimates.
/// </summary>
public class BadgeService(IStatAggregator stats) : IBadgeService
{
    public const int MinSeasons = 3;
    public const int RegularGames = 25;
    public const int TopQuartilePercentile = 75;

    public IReadOnlyList<Badge> Evaluate(IEnumerable<StatLine> lines, int? pointsPercentile)
    {
        var list = lines.ToList();
        var latest = stats.LatestLine(list);
        if (latest is null) return [];

        var badges = new List<Badge>();
        var mix = stats.SourceMix(list);
        if (mix[StatSource.CoachConfirmed] > 0)
            badges.Add(new Badge("coach-confirmed", StatSource.CoachConfirmed, mix[StatSource.CoachConfirmed]));
        if (mix[StatSource.LeagueVerifiedMock] > 0)
            badges.Add(new Badge("league-verified", StatSource.LeagueVerifiedMock, mix[StatSource.LeagueVerifiedMock]));
        if (list.Count >= MinSeasons)
            badges.Add(new Badge("multi-season", null, list.Count));
        if (latest.Games >= RegularGames)
            badges.Add(new Badge("regular", latest.Source, latest.Games));
        var trend = stats.Trend(list);
        if (trend.Direction == TrendDirection.Up)
            badges.Add(new Badge("rising", latest.Source, (int)Math.Round(trend.ChangePct!.Value, MidpointRounding.AwayFromZero)));
        if (pointsPercentile >= TopQuartilePercentile)
            badges.Add(new Badge("top-quartile", latest.Source, pointsPercentile.Value));
        return badges;
    }
}
