using Ballpass.Web.Components;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents();

// Greek by default, English on request (cookie). Resources/SharedResource{,.en}.resx
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    CultureInfo[] cultures = [new("el"), new("en")];
    options.DefaultRequestCulture = new RequestCulture("el");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.RequestCultureProviders = [new CookieRequestCultureProvider()];
});
// Keep Greek letters as-is in HTML instead of &#x...; entities.
builder.Services.AddWebEncoders(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Resolved lazily (on first DbContext use) so a missing secret fails with a clear message
// without blocking pages that need no database.
builder.Services.AddDbContext<BallpassDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Ballpass")
        ?? throw new InvalidOperationException(
            "Connection string 'ConnectionStrings:Ballpass' is missing. Run `make setup` to create the database and store it in user-secrets.");
    options.UseNpgsql(connectionString);
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.AddHealthChecks().AddDbContextCheck<BallpassDbContext>();

var app = builder.Build();

// `make reset` / `make setup`: migrate + reseed demo data, then exit without starting the web server.
// Normal startup never migrates (keeps /health 503 behaviour when the DB is down).
if (args.Contains("--reset-demo-data"))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    await seeder.ResetAsync();

    var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
    app.Logger.LogInformation(
        "Demo data reset: {Clubs} clubs, {Coaches} coaches, {Athletes} athletes, {Seasons} seasons, {StatLines} stat lines",
        await db.Clubs.CountAsync(), await db.Coaches.CountAsync(), await db.Athletes.CountAsync(),
        await db.AthleteSeasons.CountAsync(), await db.StatLines.CountAsync());
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseRequestLocalization();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapGet("/culture/set", (string? culture, string? redirectUri, HttpContext http) =>
{
    if (culture is "el" or "en")
    {
        http.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), SameSite = SameSiteMode.Lax, IsEssential = true });
    }
    // Open-redirect guard: local absolute paths only.
    var local = !string.IsNullOrEmpty(redirectUri) && redirectUri[0] == '/'
        && (redirectUri.Length == 1 || (redirectUri[1] != '/' && redirectUri[1] != '\\'));
    return Results.Redirect(local ? redirectUri! : "/");
});
app.MapRazorComponents<App>();

app.Run();

public partial class Program;
