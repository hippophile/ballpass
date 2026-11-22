using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Tests;

// Spec: openspec/specs/demo-data/spec.md — "Idempotent demo reset".
// Own database (ballpass_seedtest) so dropping it never disturbs the app factory's ballpass_test.
[Collection("db")]
public class DemoDataSeederTests
{
    [Fact]
    public async Task Reset_twice_gives_exact_counts_not_doubled()
    {
        var options = new DbContextOptionsBuilder<BallpassDbContext>()
            .UseNpgsql(TestDb.ConnectionString("ballpass_seedtest"))
            .Options;
        await using var db = new BallpassDbContext(options);
        await db.Database.EnsureDeletedAsync();

        var seeder = new DemoDataSeeder(db, TimeProvider.System);
        await seeder.ResetAsync();
        await seeder.ResetAsync();

        Assert.Equal(300, await db.Athletes.CountAsync());
        Assert.Equal(20, await db.Clubs.CountAsync());
        Assert.Equal(30, await db.Coaches.CountAsync());
        Assert.True(await db.Athletes.AllAsync(a => a.IsDemo));
        var generated = DemoDataGenerator.Generate(42);
        Assert.Equal(generated.Interests.Count, await db.Interests.CountAsync());
        Assert.Equal(generated.Shortlist.Count, await db.ShortlistEntries.CountAsync());
    }
}
