using Ballpass.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Features.Evidence;

/// <summary>Public contract for the Share package: rank of an athlete inside the demo cohort (1 = best).</summary>
public interface ICohortRank
{
    /// <summary>Rank (1 = highest value) and n (peers + the athlete) or null when the cohort is too small / athlete unknown.</summary>
    Task<(int Rank, int N)?> RankInCohort(int athleteId, EvidenceMetric metric);
}

public interface ICohortService : ICohortRank
{
    Task<CohortEvidence?> EvaluateAsync(Athlete athlete, bool confirmedOnly = false);
}

/// <summary>Pure cohort math (no database). Thresholds are our estimates and are labelled so in the UI.</summary>
public static class CohortCalculator
{
    public const int MinPeers = 20;
    public const int MinGames = 10;

    public static PositionGroup GroupOf(Position position) => position switch
    {
        Position.PG or Position.SG => PositionGroup.Guard,
        Position.SF or Position.PF => PositionGroup.Forward,
        _ => PositionGroup.Center,
    };

    public static IReadOnlyList<Position> PositionsOf(PositionGroup group) => group switch
    {
        PositionGroup.Guard => [Position.PG, Position.SG],
        PositionGroup.Forward => [Position.SF, Position.PF],
        _ => [Position.C],
    };

    /// <summary>Mid-rank: (below + 0.5 * equal) / peers, as a whole percent clamped to 1-99.</summary>
    public static int MidRankPercentile(IReadOnlyList<double> peers, double value)
    {
        if (peers.Count == 0) return 1;
        var below = peers.Count(p => p < value);
        var equal = peers.Count(p => p == value);
        var pct = (below + 0.5 * equal) / peers.Count * 100;
        return (int)Math.Clamp(Math.Round(pct, MidpointRounding.AwayFromZero), 1, 99);
    }

    /// <summary>1 + number of peers strictly above the value (ties share the better rank).</summary>
    public static int Rank(IReadOnlyList<double> peers, double value) => 1 + peers.Count(p => p > value);

    /// <summary>
    /// Peers = cohort members other than the athlete with at least <see cref="MinGames"/> games
    /// (and, with <paramref name="confirmedOnly"/>, a coach-confirmed latest line).
    /// </summary>
    public static IReadOnlyList<CohortMember> Peers(int athleteId, IReadOnlyList<CohortMember> cohort, bool confirmedOnly) =>
        cohort.Where(m => m.AthleteId != athleteId && m.Line.Games >= MinGames
                          && (!confirmedOnly || m.Line.Source == StatSource.CoachConfirmed)).ToList();

    public static CohortEvidence Evaluate(int athleteId, Gender gender, PositionGroup group, StatLine subject,
        IReadOnlyList<CohortMember> cohort, bool confirmedOnly, IStatAggregator stats)
    {
        var peers = Peers(athleteId, cohort, confirmedOnly);
        var metrics = Enum.GetValues<EvidenceMetric>().Select(metric =>
        {
            var value = stats.MetricValue(subject, metric);
            if (subject.Games < MinGames)
                return new MetricPercentile(metric, value, null, subject.Games, InsufficientReason.TooFewGames);
            if (peers.Count < MinPeers)
                return new MetricPercentile(metric, value, null, peers.Count, InsufficientReason.TooFewPeers);
            var values = peers.Select(p => stats.MetricValue(p.Line, metric)).ToList();
            return new MetricPercentile(metric, value, MidRankPercentile(values, value), peers.Count, InsufficientReason.None);
        }).ToList();
        return new CohortEvidence(gender, group, peers.Count, confirmedOnly, subject.Source, subject.Games, metrics);
    }
}

// Uses its own DbContext (own scope): in static SSR a component renders while its parent's async init is still
// running, so sharing the request's scoped context with the host page could run two queries on it at once.
public class CohortService(IServiceScopeFactory scopes, IStatAggregator stats) : ICohortService
{
    // Scoped service: the compare page evaluates two athletes, often from the same cohort.
    private readonly Dictionary<(Gender, PositionGroup), IReadOnlyList<CohortMember>> cache = [];

    public async Task<CohortEvidence?> EvaluateAsync(Athlete athlete, bool confirmedOnly = false)
    {
        var subject = stats.LatestLine(athlete.Stats);
        if (subject is null) return null;
        var group = CohortCalculator.GroupOf(athlete.Position);
        var cohort = await LoadAsync(athlete.Gender, group);
        return CohortCalculator.Evaluate(athlete.Id, athlete.Gender, group, subject, cohort, confirmedOnly, stats);
    }

    public async Task<(int Rank, int N)?> RankInCohort(int athleteId, EvidenceMetric metric)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        var athlete = await db.Athletes.AsNoTracking().Include(a => a.Stats)
            .FirstOrDefaultAsync(a => a.Id == athleteId && a.IsDemo);
        var subject = athlete is null ? null : stats.LatestLine(athlete.Stats);
        if (athlete is null || subject is null || subject.Games < CohortCalculator.MinGames) return null;

        var cohort = await LoadAsync(athlete.Gender, CohortCalculator.GroupOf(athlete.Position));
        var peers = CohortCalculator.Peers(athlete.Id, cohort, confirmedOnly: false);
        if (peers.Count < CohortCalculator.MinPeers) return null;

        var rank = CohortCalculator.Rank(peers.Select(p => stats.MetricValue(p.Line, metric)).ToList(), stats.MetricValue(subject, metric));
        return (rank, peers.Count + 1);
    }

    /// <summary>Every demo athlete of the gender and position group, each with the latest season line.</summary>
    private async Task<IReadOnlyList<CohortMember>> LoadAsync(Gender gender, PositionGroup group)
    {
        if (cache.TryGetValue((gender, group), out var hit)) return hit;
        var positions = CohortCalculator.PositionsOf(group);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        var rows = await db.Athletes.AsNoTracking()
            .Where(a => a.IsDemo && a.Gender == gender && positions.Contains(a.Position))
            .SelectMany(a => a.Stats)
            .ToListAsync();
        var members = rows.GroupBy(l => l.AthleteId)
            .Select(g => new CohortMember(g.Key, stats.LatestLine(g)!))
            .ToList();
        return cache[(gender, group)] = members;
    }
}
