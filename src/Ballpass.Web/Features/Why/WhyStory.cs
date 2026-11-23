using Ballpass.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Features.Why;

/// <summary>The three live numbers of the proof strip: the same queries as the home counters (flow-release §4.1).</summary>
public sealed record LiveCounts(int Looking, int Interests, int Connections)
{
    public static async Task<LiveCounts> LoadAsync(BallpassDbContext db) => new(
        await db.Athletes.CountAsync(a => a.IsDemo && a.LookingForTeam),
        await db.Interests.CountAsync(),
        await db.Interests.CountAsync(i => i.Reply == InterestReply.Accepted));
}

/// <summary>Source pages behind the figures on /why (docs/research/problem.md, all opened 2026-11-23). A figure without a URL is not shown.</summary>
public static class WhySources
{
    public const string StudentsMove = "https://www.alfavita.gr/ekpaideysi/152884_megali-meiosi-tis-esoterikis-foititikis-metanasteysis";
    public const string Agents = "https://www.eurohoops.net/el/ellada/1836945/sindesmou-ellinon-antiprosopon-kalathosferiston-ta-30-energa-ke-adiodotimena-meli/";
    public const string WomenCoverage = "https://webarchive.unesco.org/web/20230104165710/https://en.unesco.org/themes/gender-equality-sports-media";
}

/// <summary>
/// Who the "Με το Ballpass" previews show: the featured personas from <c>Demo:Featured</c>, or, when unset, the first demo athlete
/// looking for a team and the first demo head coach of a women's club. Either may be null on an empty database.
/// </summary>
public sealed record StoryCast(Athlete? Athlete, Coach? Coach)
{
    public static async Task<StoryCast> LoadAsync(BallpassDbContext db, Flow.FeaturedPersonas featured)
    {
        var athlete = featured.Athlete ?? await db.Athletes.AsNoTracking().Include(a => a.CurrentClub)
            .Where(a => a.IsDemo && a.LookingForTeam && a.Gender == Gender.Female).OrderBy(a => a.Id).FirstOrDefaultAsync();
        var coach = featured.Coach ?? await db.Coaches.AsNoTracking().Include(c => c.Club)
            .Where(c => c.IsDemo && c.Role == CoachRole.Head && c.Club!.Gender == Gender.Female).OrderBy(c => c.Id).FirstOrDefaultAsync();
        return new(athlete, coach);
    }
}
