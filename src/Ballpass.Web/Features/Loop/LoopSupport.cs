using System.Globalization;
using System.Security.Claims;
using System.Text;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Features.Loop;

/// <summary>Config section Demo. Off by default (production); appsettings.Development.json turns the persona switch and /demo/reset on.</summary>
public class DemoOptions
{
    public bool Enabled { get; set; }
}

/// <summary>Fictional contact of a demo club: never a real mailbox (.invalid is reserved and cannot resolve).</summary>
public static class ClubContact
{
    private static readonly Dictionary<string, string> Digraphs = new() { ["ου"] = "ou", ["αυ"] = "av", ["ευ"] = "ev", ["μπ"] = "b", ["ντ"] = "d", ["γκ"] = "g", ["τσ"] = "ts", ["τζ"] = "tz" };

    private static readonly Dictionary<char, string> Letters = new()
    {
        ['α'] = "a", ['β'] = "v", ['γ'] = "g", ['δ'] = "d", ['ε'] = "e", ['ζ'] = "z", ['η'] = "i", ['θ'] = "th", ['ι'] = "i", ['κ'] = "k",
        ['λ'] = "l", ['μ'] = "m", ['ν'] = "n", ['ξ'] = "x", ['ο'] = "o", ['π'] = "p", ['ρ'] = "r", ['σ'] = "s", ['ς'] = "s", ['τ'] = "t",
        ['υ'] = "y", ['φ'] = "f", ['χ'] = "ch", ['ψ'] = "ps", ['ω'] = "o",
    };

    public static string Slug(string clubName)
    {
        var text = new string(clubName.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        var sb = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (i + 1 < text.Length && Digraphs.TryGetValue(text.Substring(i, 2), out var di)) { sb.Append(di); i++; continue; }
            if (Letters.TryGetValue(text[i], out var l)) sb.Append(l);
            else if (text[i] is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(text[i]);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "club" : slug;
    }

    public static string Email(string clubName) => $"contact@{Slug(clubName)}.demo.invalid";
}

/// <summary>Claims for a demo persona. Same shape as the /demo/login endpoint, so every page treats both the same.</summary>
public static class PersonaClaims
{
    public const string SwitchFromCoach = "switch_from_coach";

    public static List<Claim> ForCoach(Coach coach) =>
    [
        new(ClaimTypes.NameIdentifier, $"coach:{coach.Id}"),
        new(ClaimTypes.Name, $"{coach.FirstName} {coach.LastName}"),
        new("name_latin", $"{coach.FirstNameLatin} {coach.LastNameLatin}"),
        new(ClaimTypes.Role, "Coach"),
        new("gender", GreekNames.IsFemaleFirstName(coach.FirstName) ? "F" : "M"),
        new("club_id", coach.ClubId.ToString()),
        new("entity_id", coach.Id.ToString()),
    ];

    /// <param name="fromCoachId">The coach to return to; remembered in a claim.</param>
    public static List<Claim> ForAthlete(Athlete athlete, int? fromCoachId = null)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, $"athlete:{athlete.Id}"),
            new(ClaimTypes.Name, $"{athlete.FirstName} {athlete.LastName}"),
            new("name_latin", $"{athlete.FirstNameLatin} {athlete.LastNameLatin}"),
            new(ClaimTypes.Role, "Athlete"),
            new("gender", athlete.Gender == Gender.Female ? "F" : "M"),
            new("slug", athlete.Slug),
            new("entity_id", athlete.Id.ToString()),
        ];
        if (fromCoachId is { } id) claims.Add(new Claim(SwitchFromCoach, id.ToString()));
        return claims;
    }
}

/// <summary>Unread replies for the coach nav link. Scoped: both nav renderings of a request share one query.</summary>
public class LoopService(IServiceScopeFactory scopes)
{
    private readonly Dictionary<int, Task<int>> unread = [];

    public Task<int> UnreadRepliesAsync(int coachId)
    {
        lock (unread)
        {
            if (!unread.TryGetValue(coachId, out var task)) unread[coachId] = task = Load(coachId);
            return task;
        }
    }

    private async Task<int> Load(int coachId)
    {
        // Own scope: the layout renders while the page's own queries may still be running on the request's context.
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        return await db.Interests.CountAsync(i => i.CoachId == coachId && i.Reply != InterestReply.None && i.ReplyReadAt == null);
    }
}
