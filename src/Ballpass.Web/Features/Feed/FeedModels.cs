using System.Security.Claims;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Feed;

/// <summary>Weights and limits, section "Feed" of appsettings.json. Defaults match the plan (estimates, not measured).</summary>
public class FeedOptions
{
    public int BoostLookingForTeamHours { get; set; } = 12;
    public int BoostTryoutHours { get; set; } = 12;
    public int BoostCoachConfirmedHours { get; set; } = 6;
    public int ReportHideThreshold { get; set; } = 3;
    public int PostCooldownSeconds { get; set; } = 60;
}

public static class FeedLimits
{
    public const int PageSize = 10;
    public const int MaxItems = 60;
    public const int TextMax = 280;
    public const int NoteMax = 140;
    public const int TitleMax = 80;
    public const int PlaceMax = 120;
    public const int PositionsMax = 40;
    public const int RegionMax = 80;
}

public enum FeedTab { ForYou, Following }

/// <summary>Who is looking at the feed. Key is null for anonymous visitors.</summary>
public record FeedViewer(string? Key, string? Role, int EntityId, int? ClubId)
{
    public static readonly FeedViewer Anonymous = new(null, null, 0, null);
    public bool SignedIn => Key is not null;
    public bool IsCoach => Role == "Coach";
    public bool IsAthlete => Role == "Athlete";

    public static FeedViewer From(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return Anonymous;
        var role = user.IsInRole("Coach") ? "Coach" : user.IsInRole("Athlete") ? "Athlete" : null;
        if (role is null || !int.TryParse(user.FindFirstValue("entity_id"), out var id)) return Anonymous;
        int? clubId = int.TryParse(user.FindFirstValue("club_id"), out var c) ? c : null;
        return new FeedViewer(FeedKeys.Make(role == "Coach" ? AuthorKind.Coach : AuthorKind.Athlete, id), role, id, clubId);
    }
}

public static class FeedKeys
{
    public static string Make(AuthorKind kind, int id) => $"{kind.ToString().ToLowerInvariant()}:{id}";

    public static bool TryParse(string? key, out AuthorKind kind, out int id)
    {
        kind = default; id = 0;
        var parts = key?.Split(':');
        return parts is { Length: 2 }
            && Enum.TryParse(parts[0], ignoreCase: true, out kind)
            && int.TryParse(parts[1], out id);
    }

    /// <summary>Postgres timestamptz keeps microseconds: truncate so a cursor value compares equal to the stored one.</summary>
    public static DateTimeOffset Truncate(DateTimeOffset t) => new(t.UtcTicks - t.UtcTicks % 10, TimeSpan.Zero);
}

public static class FeedRanking
{
    /// <summary>Ranking without ML: looking-for-team, tryout and coach-confirmed stat posts sort as if they were a few hours newer.</summary>
    public static TimeSpan Boost(PostType type, StatSource? source, FeedOptions o) => type switch
    {
        PostType.LookingForTeam => TimeSpan.FromHours(o.BoostLookingForTeamHours),
        PostType.TryoutCall => TimeSpan.FromHours(o.BoostTryoutHours),
        PostType.StatUpdate when source is StatSource.CoachConfirmed or StatSource.LeagueVerifiedMock => TimeSpan.FromHours(o.BoostCoachConfirmedHours),
        _ => TimeSpan.Zero,
    };
}

public static class Safeguards
{
    /// <summary>Conservative: only birth years whose 18th birthday is certainly past (same rule as the demo data generator).</summary>
    public static bool IsAdult(int birthYear, DateTimeOffset now) => birthYear + 18 < now.Year;

    public static readonly string[] ReportReasons = ["spam", "inappropriate", "fake", "minor", "other"];

    private static readonly Regex Url = new(@"(https?://|www\.)\S+|\b[\w-]+(\.[\w-]+)*\.(com|gr|org|net|io|eu|me|ly|tv)\b(/\S*)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    /// <summary>Removes URLs, control characters and repeated whitespace; null when nothing is left.</summary>
    public static string? Clean(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = Url.Replace(input, " ");
        s = new string(s.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length == 0 ? null : s;
    }
}

/// <summary>Input of the composer. Only the fields of <see cref="Type"/> are read.</summary>
public record NewPost(PostType Type)
{
    public string? Text { get; init; }
    public string? Title { get; init; }
    /// <summary>Looking-for-team switch: true posts and turns the profile flag on, false only turns it off.</summary>
    public bool LookingOn { get; init; } = true;
    public Position? Position { get; init; }
    public string? Region { get; init; }
    public DateOnly? AvailableFrom { get; init; }
    public DateOnly? EventDate { get; init; }
    public string? Place { get; init; }
    public string? Positions { get; init; }
    public DateOnly? Deadline { get; init; }
}

public enum PostOutcome { Posted, SwitchedOff, Forbidden, Minor, RateLimited, Invalid, TooLong }

public enum ModOutcome { Ok, NotFound, Forbidden, Invalid }

public record FeedItem(
    Post Post,
    string NameEl,
    string NameLatin,
    string RoleKey,
    string? Slug,
    string? ClubName,
    bool IsOwn,
    bool KudoedByMe,
    bool Following,
    bool InterestSent);

public record FeedPageResult(IReadOnlyList<FeedItem> Items, string? NextCursor);
