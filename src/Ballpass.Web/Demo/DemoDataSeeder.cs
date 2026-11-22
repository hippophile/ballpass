using Ballpass.Web.Data;
using Ballpass.Web.Features.Media;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Demo;

// Applies migrations, then replaces all demo rows with the generated set in one transaction.
public class DemoDataSeeder(BallpassDbContext db, TimeProvider time)
{
    public const int Seed = 42;

    public virtual async Task ResetAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Children first (FKs). Interests / shortlist rows reference demo coaches and athletes.
        await db.Interests.Where(i => db.Athletes.Any(a => a.Id == i.AthleteId && a.IsDemo)
            || db.Coaches.Any(c => c.Id == i.CoachId && c.IsDemo)).ExecuteDeleteAsync(ct);
        await db.ShortlistEntries.Where(i => db.Athletes.Any(a => a.Id == i.AthleteId && a.IsDemo)
            || db.Coaches.Any(c => c.Id == i.CoachId && c.IsDemo)).ExecuteDeleteAsync(ct);
        // Stat lines / seasons also go if they belong to a demo athlete.
        await db.StatLines.Where(s => db.Athletes.Any(a => a.Id == s.AthleteId && a.IsDemo)).ExecuteDeleteAsync(ct);
        await db.AthleteSeasons.Where(s => db.Athletes.Any(a => a.Id == s.AthleteId && a.IsDemo)).ExecuteDeleteAsync(ct);
        await db.Athletes.Where(a => a.IsDemo).ExecuteDeleteAsync(ct);
        await db.Coaches.Where(c => c.IsDemo).ExecuteDeleteAsync(ct);
        await db.Clubs.Where(c => c.IsDemo).ExecuteDeleteAsync(ct);

        var now = time.GetUtcNow();
        var data = DemoDataGenerator.Generate(Seed);
        InterestLifecycleSeeder.Apply(data.Interests, now, Seed);
        db.Clubs.AddRange(data.Clubs);
        db.Coaches.AddRange(data.Coaches);
        db.Athletes.AddRange(data.Athletes);
        db.Interests.AddRange(data.Interests);
        db.ShortlistEntries.AddRange(data.Shortlist);
        await db.SaveChangesAsync(ct);
        await FeedSeeder.ReplaceAsync(db, data, now, ct);
        await MediaSeeder.ReplaceAsync(db, ct);

        await tx.CommitAsync(ct);
    }
}
