using Ballpass.Web.Demo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ballpass.Web.Tests;

/// <summary>Connection strings for test databases, derived from the dev user-secret (never printed).</summary>
public static class TestDb
{
    public static string ConnectionString(string database)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();
        var dev = config.GetConnectionString("Ballpass")
            ?? throw new InvalidOperationException("Run `make setup` first (ConnectionStrings:Ballpass missing).");
        return new NpgsqlConnectionStringBuilder(dev) { Database = database }.ConnectionString;
    }
}

/// <summary>App wired to DB ballpass_test, seeded once with the seed-42 demo data.</summary>
public class BallpassAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Ballpass", TestDb.ConnectionString("ballpass_test"));

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().ResetAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public HttpClient Client(string? culture = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (culture is not null)
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Culture={Uri.EscapeDataString($"c={culture}|uic={culture}")}");
        return client;
    }
}

[CollectionDefinition("db")]
public class DbCollection : ICollectionFixture<BallpassAppFactory>;
