using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/add-demo-data/specs/demo-data/spec.md — "Idempotent demo reset".
// Uses a separate database (ballpass_test) so the dev demo DB is never touched by tests.
public class DemoDataSeederTests
{
    [Fact]
    public async Task Reset_twice_gives_exact_counts_not_doubled()
    {
        await using var db = CreateTestDbContext();
        await db.Database.EnsureDeletedAsync();

        var seeder = new DemoDataSeeder(db);
        await seeder.ResetAsync();
        await seeder.ResetAsync();

        Assert.Equal(300, await db.Athletes.CountAsync());
        Assert.Equal(20, await db.Clubs.CountAsync());
        Assert.Equal(30, await db.Coaches.CountAsync());
        Assert.True(await db.Athletes.AllAsync(a => a.IsDemo));
    }

    private static BallpassDbContext CreateTestDbContext()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();
        var devConnection = config.GetConnectionString("Ballpass")
            ?? throw new InvalidOperationException("Run `make setup` first (ConnectionStrings:Ballpass missing).");
        var builder = new NpgsqlConnectionStringBuilder(devConnection) { Database = "ballpass_test" };

        var options = new DbContextOptionsBuilder<BallpassDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .Options;
        return new BallpassDbContext(options);
    }
}
