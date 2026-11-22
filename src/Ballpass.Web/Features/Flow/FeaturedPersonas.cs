using Ballpass.Web.Data;
using Ballpass.Web.Features.Loop;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Features.Flow;

/// <summary>
/// The personas the 60-second demo starts from (home doors, /demo featured row), resolved from <c>Demo:Featured</c> configuration:
/// the coach by first + last name, the athlete by slug (SPEC §8 O7). Either is null when unset or not in the database; callers
/// then fall back to a link to /demo. Static on purpose: no DI registration (Program.cs is not ours to touch).
/// </summary>
public sealed record FeaturedPersonas(Coach? Coach, Athlete? Athlete)
{
    /// <summary>/match's own shortcut for <c>Demo:Featured:NeedQuery</c> (WP-B): the coach lands on a scored, prefilled need.</summary>
    public const string CoachReturnUrl = "/match?need=featured";

    public const string AthleteReturnUrl = "/me/inbox";

    public static async Task<FeaturedPersonas> LoadAsync(BallpassDbContext db, FeaturedOptions featured)
    {
        var coach = string.IsNullOrWhiteSpace(featured.CoachFirstName) ? null : await db.Coaches.AsNoTracking().Include(c => c.Club)
            .Where(c => c.IsDemo && c.FirstName == featured.CoachFirstName && c.LastName == featured.CoachLastName)
            .OrderBy(c => c.Id).FirstOrDefaultAsync();
        var athlete = string.IsNullOrWhiteSpace(featured.AthleteSlug) ? null : await db.Athletes.AsNoTracking().Include(a => a.CurrentClub)
            .FirstOrDefaultAsync(a => a.IsDemo && a.Slug == featured.AthleteSlug);
        return new(coach, athlete);
    }
}
