using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Evidence;

/// <summary>Metrics with a percentile bar. Counting stats are per 40 minutes; FG% is used as is.</summary>
public enum EvidenceMetric { Points, Rebounds, Assists, Steals, FgPct }

/// <summary>Cohort grouping: Guard = PG+SG, Forward = SF+PF, Center = C.</summary>
public enum PositionGroup { Guard, Forward, Center }

public enum TrendDirection { Up, Down, Flat, NotAvailable }

public enum InsufficientReason { None, TooFewPeers, TooFewGames, TooFewMinutes }

public record StatValues(double Points, double Rebounds, double Assists, double Steals, double Blocks, double FgPct);

/// <summary>PerGame and Per40 for one season line or for a career. Games and TotalMinutes are sums.</summary>
public record StatAggregate(int Games, double TotalMinutes, StatValues PerGame, StatValues Per40);

public record TrendResult(TrendDirection Direction, decimal? ChangePct, string? LatestSeason, string? PreviousSeason);

/// <summary>One slot on the season axis. Line is null for a season the athlete has no stat line for (breaks the line).</summary>
public record SeasonSlot(string Season, StatLine? Line);

/// <summary>Percentile of one metric. Percentile is null when Reason is not None (no fill is drawn).</summary>
public record MetricPercentile(EvidenceMetric Metric, double Value, int? Percentile, int N, InsufficientReason Reason);

public record CohortEvidence(Gender Gender, PositionGroup Group, int PeerCount, bool ConfirmedOnly,
    StatSource SubjectSource, int SubjectGames, IReadOnlyList<MetricPercentile> Metrics)
{
    public MetricPercentile? Get(EvidenceMetric metric) => Metrics.FirstOrDefault(m => m.Metric == metric);
}

/// <summary>A peer in a cohort: the athlete id plus that athlete's latest stat line.</summary>
public record CohortMember(int AthleteId, StatLine Line);

/// <summary>Rule badge. Rule is a stable code, Args fill the localized text of resource key Badge_{Rule}.</summary>
public record Badge(string Rule, StatSource? Source, params object[] Args);
