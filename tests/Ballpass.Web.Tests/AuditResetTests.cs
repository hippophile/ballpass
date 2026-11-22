using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Loop;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-audit-fixes/specs/loop/spec.md (reset needs a persona, is rate limited, defaults)
// The seeder is replaced by a counting fake so the persona survives the "reset": the sign-out is then the only reason
// the next request is anonymous, and the same persona can come back for the rate-limit check.
[Collection("db")]
public class AuditResetTests(BallpassAppFactory app)
{
    private readonly FeedHarness h = new(app);

    private sealed class ResetCounter { public int Calls; }

    private sealed class CountingSeeder(BallpassDbContext db, TimeProvider time, ResetCounter counter) : DemoDataSeeder(db, time)
    {
        public override Task ResetAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref counter.Calls);
            return Task.CompletedTask;
        }
    }

    // Own host per test: its own rate-limiter state, so tests never starve each other of the one reset per minute.
    private (HttpClient Client, ResetCounter Counter) Host()
    {
        var counter = new ResetCounter();
        var factory = app.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddScoped<DemoDataSeeder>(sp => new CountingSeeder(sp.GetRequiredService<BallpassDbContext>(), sp.GetRequiredService<TimeProvider>(), counter))));
        return (factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }), counter);
    }

    private static async Task<string> Html(HttpClient c, string path) => await FeedHarness.Html(c, path);

    private async Task Login(HttpClient client, int coachId)
    {
        var token = FeedHarness.Token(await Html(client, "/demo"));
        var response = await client.PostAsync("/demo/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["personaType"] = "coach", ["personaId"] = coachId.ToString(), ["returnUrl"] = "", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostReset(HttpClient client)
    {
        var token = FeedHarness.Token(await Html(client, "/demo/reset"));
        return await client.PostAsync("/demo/reset", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
    }

    [Fact]
    public async Task Anonymous_reset_goes_to_the_persona_chooser_and_resets_nothing()
    {
        var (client, counter) = Host();

        var response = await PostReset(client);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/demo", response.Headers.Location!.OriginalString);
        Assert.Equal(0, counter.Calls);
    }

    [Fact]
    public async Task Second_reset_by_the_same_persona_within_a_minute_is_429_with_a_page()
    {
        var (client, counter) = Host();
        var coachId = h.CoachId(5);
        await Login(client, coachId);

        var first = await PostReset(client);
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.StartsWith("/?demo=reset", first.Headers.Location!.OriginalString);

        await Login(client, coachId);   // reset signs out; the persona still exists (fake seeder), so it can come back
        var second = await PostReset(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        var body = WebUtility.HtmlDecode(await second.Content.ReadAsStringAsync());
        Assert.Contains("<h1", body);
        Assert.Contains("μία φορά το λεπτό", body);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Another_persona_has_its_own_allowance()
    {
        var (client, counter) = Host();
        await Login(client, h.CoachId(6));
        Assert.Equal(HttpStatusCode.Redirect, (await PostReset(client)).StatusCode);

        await Login(client, h.CoachId(7));
        var other = await PostReset(client);

        Assert.Equal(HttpStatusCode.Redirect, other.StatusCode);
        Assert.Equal(2, counter.Calls);
    }

    // Surviving mutation N05: removing the sign-out left every test green because the real reset deletes the persona anyway.
    [Fact]
    public async Task Reset_signs_the_caller_out_even_when_the_persona_still_exists()
    {
        var (client, _) = Host();
        await Login(client, h.CoachId(8));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/search")).StatusCode);

        var response = await PostReset(client);

        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];
        var auth = cookies.Single(c => c.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal));
        Assert.Matches(@"^\.AspNetCore\.Cookies=;", auth);                 // value cleared
        Assert.Contains("expires=Thu, 01 Jan 1970", auth, StringComparison.OrdinalIgnoreCase);
        var next = await client.GetAsync("/search");
        Assert.Equal(HttpStatusCode.Redirect, next.StatusCode);              // anonymous: sent to the chooser
        Assert.StartsWith("/demo", next.Headers.Location!.OriginalString);
    }

    [Fact]
    public void Demo_is_off_by_default_and_on_in_the_development_test_host()
    {
        var shipped = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json")).Build();
        Assert.False(shipped.GetValue<bool>("Demo:Enabled"));
        Assert.False(new DemoOptions().Enabled);

        Assert.True(app.Services.GetRequiredService<IHostEnvironment>().IsDevelopment());
        Assert.True(app.Services.GetRequiredService<IOptions<DemoOptions>>().Value.Enabled);
    }
}
