using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Find.Fit;

public enum FitPriority { None, Rebounding, Scoring, Playmaking }

public enum FitCriterion { Height, Age, Region, League, Stat }

/// <summary>What the coach asked for. Positions and ConfirmedOnly are hard filters; everything else is a soft, weighted criterion.</summary>
public record FitNeed
{
    public IReadOnlyList<Position> Positions { get; init; } = [];
    public int? MinHeight { get; init; }
    public int? MaxHeight { get; init; }
    public int? MinAge { get; init; }
    public int? MaxAge { get; init; }
    public string? Region { get; init; }
    public League? League { get; init; }
    public FitPriority Priority { get; init; }
    public bool ConfirmedOnly { get; init; }

    public bool WantsHeight => MinHeight is not null || MaxHeight is not null;
    public bool WantsAge => MinAge is not null || MaxAge is not null;
}

/// <summary>Per-game stat line of the athlete's latest season. Source is the source of that line.</summary>
public record FitStat(string Season, int Games, decimal Points, decimal Rebounds, decimal Assists, StatSource Source);

/// <summary>Plain data the scorer needs (no EF entities, so the scorer stays pure).</summary>
public record FitCandidate(string Slug, Position Position, int HeightCm, int Age, string Region, League? League, FitStat? Stat);

public enum ReasonState { Met, Partial, Missed }

/// <summary>
/// One templated reason. Actual is the athlete's value (cm, years, per-game stat); ActualKey is the region/league key.
/// The UI turns this into text; Source is always set (profile fields are self-reported, stats carry their stat line's source).
/// </summary>
public record FitReason(FitCriterion Criterion, ReasonState State, StatSource Source, double Actual, string? ActualKey, int? TopPercent, Position? CohortPosition);

public record FitResult(
    FitCandidate Candidate,
    int? FitPercent,
    double Coverage,
    IReadOnlyList<FitReason> Reasons,
    IReadOnlyList<FitCriterion> Missing)
{
    public bool Partial => Missing.Count > 0;
}

public record FitRanking(IReadOnlyList<FitResult> Results, int Hidden);

public record TrustShares(int SelfReported, int CoachConfirmed, int LeagueVerified);

/// <summary>Default weights (hand-chosen estimates, shown to the coach as "default weights"). Bound from appsettings "Fit".</summary>
public class FitOptions
{
    public const string Section = "Fit";

    public FitWeights Weights { get; set; } = new();

    /// <summary>A stat line with fewer games than this counts as missing.</summary>
    public int MinGames { get; set; } = 10;

    /// <summary>Height score falls linearly to 0 this many cm outside the requested range.</summary>
    public double HeightFalloffCm { get; set; } = 10;

    /// <summary>Age score falls linearly to 0 this many years outside the requested range.</summary>
    public double AgeFalloffYears { get; set; } = 4;
}

public class FitWeights
{
    public double Height { get; set; } = 0.30;
    public double Stat { get; set; } = 0.30;
    public double Region { get; set; } = 0.15;
    public double Age { get; set; } = 0.15;
    public double League { get; set; } = 0.10;
}
