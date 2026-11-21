using Ballpass.Web;
using Ballpass.Web.Components;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Find;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents();

// Demo persona switch: cookie auth, no Identity tables (real accounts are pilot work).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/demo";
        options.AccessDeniedPath = "/denied";
        options.ReturnUrlParameter = "ReturnUrl";
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Relative Location (the default is absolute): works behind a proxy and keeps redirects host-agnostic.
        // A demo reset deletes the personas: reject sessions whose coach/athlete row no longer exists.
        options.Events.OnValidatePrincipal = async ctx =>
        {
            var role = ctx.Principal?.FindFirstValue(ClaimTypes.Role);
            if (!int.TryParse(ctx.Principal?.FindFirstValue("entity_id"), out var id)) return;
            var db = ctx.HttpContext.RequestServices.GetRequiredService<BallpassDbContext>();
            var exists = role switch
            {
                "Coach" => await db.Coaches.AnyAsync(c => c.Id == id && c.IsDemo),
                "Athlete" => await db.Athletes.AnyAsync(a => a.Id == id && a.IsDemo),
                _ => true,
            };
            if (exists) return;
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        };
        options.Events.OnRedirectToLogin = ctx =>
        {
            var returnUrl = Uri.EscapeDataString(ctx.Request.PathBase + ctx.Request.Path + ctx.Request.QueryString);
            ctx.Response.Redirect($"{options.LoginPath}?{options.ReturnUrlParameter}={returnUrl}");
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy("CoachOnly", policy => policy.RequireRole("Coach")));
builder.Services.AddCascadingAuthenticationState();

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
builder.Services.AddFind();
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
        "Demo data reset: {Clubs} clubs, {Coaches} coaches, {Athletes} athletes, {Seasons} seasons, {StatLines} stat lines, {Interests} interests, {Shortlist} shortlist entries",
        await db.Clubs.CountAsync(), await db.Coaches.CountAsync(), await db.Athletes.CountAsync(),
        await db.AthleteSeasons.CountAsync(), await db.StatLines.CountAsync(),
        await db.Interests.CountAsync(), await db.ShortlistEntries.CountAsync());
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
// 403 -> Denied page, anything else -> NotFound page (see Components/Pages/Status.razor).
app.UseStatusCodePagesWithReExecute("/status/{0}", createScopeForStatusCodePages: true);
// Error/status pages are re-executed with the original method. A failed POST (e.g. 400 from a missing token) would
// re-run the page as POST and the component endpoint would try to read a form body that may not exist → 500.
// Re-run them as GET instead.
app.Use((context, next) =>
{
    if (context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>() is not null
        || context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>() is not null)
        context.Request.Method = HttpMethods.Get;
    return next();
});
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
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
    return Results.Redirect(LocalRedirect.IsSafe(redirectUri) ? redirectUri! : "/");
});

// Explicit antiforgery check (single source): invalid/missing token -> 400, never a 500.
static async Task<bool> TokenValidAsync(IAntiforgery antiforgery, HttpContext http)
{
    try { await antiforgery.ValidateRequestAsync(http); return true; }
    catch (AntiforgeryValidationException) { return false; }
}

static bool IsUniqueViolation(DbUpdateException e) => e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

app.MapPost("/demo/login", async ([FromForm] string? personaType, [FromForm] int personaId, [FromForm] string? returnUrl,
    BallpassDbContext db, HttpContext http, IAntiforgery antiforgery) =>
{
    if (!await TokenValidAsync(antiforgery, http)) return Results.BadRequest();
    List<Claim> claims;
    string home;
    if (personaType == "coach")
    {
        var coach = await db.Coaches.AsNoTracking().FirstOrDefaultAsync(c => c.Id == personaId && c.IsDemo);
        if (coach is null) return Results.BadRequest();
        claims =
        [
            new(ClaimTypes.NameIdentifier, $"coach:{coach.Id}"),
            new(ClaimTypes.Name, $"{coach.FirstName} {coach.LastName}"),
            new("name_latin", $"{coach.FirstNameLatin} {coach.LastNameLatin}"),
            new(ClaimTypes.Role, "Coach"),
            new("gender", GreekNames.IsFemaleFirstName(coach.FirstName) ? "F" : "M"),
            new("club_id", coach.ClubId.ToString()),
            new("entity_id", coach.Id.ToString()),
        ];
        home = "/search";
    }
    else if (personaType == "athlete")
    {
        var athlete = await db.Athletes.AsNoTracking().FirstOrDefaultAsync(a => a.Id == personaId && a.IsDemo);
        if (athlete is null) return Results.BadRequest();
        claims =
        [
            new(ClaimTypes.NameIdentifier, $"athlete:{athlete.Id}"),
            new(ClaimTypes.Name, $"{athlete.FirstName} {athlete.LastName}"),
            new("name_latin", $"{athlete.FirstNameLatin} {athlete.LastNameLatin}"),
            new(ClaimTypes.Role, "Athlete"),
            new("gender", athlete.Gender == Gender.Female ? "F" : "M"),
            new("slug", athlete.Slug),
            new("entity_id", athlete.Id.ToString()),
        ];
        home = $"/p/{athlete.Slug}";
    }
    else return Results.BadRequest();

    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    return Results.Redirect(LocalRedirect.IsSafe(returnUrl) ? returnUrl! : home);
}).DisableAntiforgery();
// Coach actions on a profile. Role is enforced here (plain 403), not only by hiding the buttons.
app.MapPost("/p/{slug}/interest", async (string slug, [FromForm] string? note, BallpassDbContext db, HttpContext http, TimeProvider time, IAntiforgery antiforgery) =>
{
    if (!await TokenValidAsync(antiforgery, http)) return Results.BadRequest();
    if (!http.User.IsInRole("Coach") || !int.TryParse(http.User.FindFirstValue("entity_id"), out var coachId))
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    var athlete = await db.Athletes.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug && a.IsDemo);
    if (athlete is null) return Results.NotFound();

    var text = note?.Trim();
    if (text is { Length: > 280 }) return Results.Redirect($"/p/{slug}?status=note-too-long");
    if (await db.Interests.AnyAsync(i => i.CoachId == coachId && i.AthleteId == athlete.Id))
        return Results.Redirect($"/p/{slug}?status=interest-exists");

    db.Interests.Add(new Interest
    {
        CoachId = coachId,
        AthleteId = athlete.Id,
        Note = string.IsNullOrEmpty(text) ? null : text,
        CreatedAt = time.GetUtcNow(),
        IsDemo = true,
    });
    try { await db.SaveChangesAsync(); }
    catch (DbUpdateException e) when (IsUniqueViolation(e))
    {
        return Results.Redirect($"/p/{slug}?status=interest-exists");
    }
    return Results.Redirect($"/p/{slug}?status=interest-sent");
}).DisableAntiforgery();
app.MapPost("/p/{slug}/shortlist", async (string slug, BallpassDbContext db, HttpContext http, TimeProvider time, IAntiforgery antiforgery) =>
{
    if (!await TokenValidAsync(antiforgery, http)) return Results.BadRequest();
    if (!http.User.IsInRole("Coach") || !int.TryParse(http.User.FindFirstValue("entity_id"), out var coachId))
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    var athlete = await db.Athletes.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug && a.IsDemo);
    if (athlete is null) return Results.NotFound();

    var entry = await db.ShortlistEntries.FirstOrDefaultAsync(s => s.CoachId == coachId && s.AthleteId == athlete.Id);
    if (entry is null)
    {
        db.ShortlistEntries.Add(new ShortlistEntry { CoachId = coachId, AthleteId = athlete.Id, CreatedAt = time.GetUtcNow(), IsDemo = true });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            // Concurrent add already created it: current state is "shortlisted".
            return Results.Redirect($"/p/{slug}?status=shortlist-added");
        }
        return Results.Redirect($"/p/{slug}?status=shortlist-added");
    }
    db.ShortlistEntries.Remove(entry);
    try { await db.SaveChangesAsync(); }
    catch (DbUpdateConcurrencyException)
    {
        // Concurrent remove already deleted it: current state is "not shortlisted".
    }
    return Results.Redirect($"/p/{slug}?status=shortlist-removed");
});
app.MapPost("/demo/logout", async (HttpContext http, IAntiforgery antiforgery) =>
{
    if (!await TokenValidAsync(antiforgery, http)) return Results.BadRequest();
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});
app.MapFind();
app.MapRazorComponents<App>();

app.Run();

public partial class Program;
