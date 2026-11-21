using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Share;

/// <summary>
/// Aggregates behind the Season Wrapped slides, over the athlete's latest season. Games = sum over that season's lines,
/// minutes = sum of games x minutes-per-game (rounded), the stat line = the season line with the most games,
/// growth = that line minus the same for the previous season on record.
/// </summary>
public sealed record SeasonWrapped(string Season, int Games, int Minutes, StatLine Line, StatLine? Previous)
{
    /// <summary>Under this many games only intro, games/minutes and outro are shown.</summary>
    public const int FullStoryMinGames = 5;

    public bool IsFullStory => Games >= FullStoryMinGames;

    public static SeasonWrapped? From(IReadOnlyCollection<StatLine> stats)
    {
        if (stats.Count == 0) return null;
        var season = stats.Max(s => s.Season)!;
        var lines = stats.Where(s => s.Season == season).ToList();
        var previousSeason = stats.Where(s => string.CompareOrdinal(s.Season, season) < 0).Select(s => s.Season).DefaultIfEmpty().Max();
        var previous = previousSeason is null ? null
            : stats.Where(s => s.Season == previousSeason).OrderByDescending(s => s.Games).First();
        return new SeasonWrapped(season, lines.Sum(s => s.Games), (int)Math.Round(lines.Sum(s => s.Games * s.Minutes)),
            lines.OrderByDescending(s => s.Games).First(), previous);
    }
}
