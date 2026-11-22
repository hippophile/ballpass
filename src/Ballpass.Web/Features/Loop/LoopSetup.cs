using System.Security.Claims;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Loop;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// Root namespace on purpose: Program.cs already imports Ballpass.Web, so it needs no extra using for AddLoop/MapLoop.
namespace Ballpass.Web;

public static class LoopSetup
{
    public static IServiceCollection AddLoop(this IServiceCollection services)
    {
        services.AddOptions<DemoOptions>().BindConfiguration("Demo");
        services.AddScoped<LoopService>();
        return services;
    }

    /// <summary>Plain-form POST endpoints. Same rules as /p/{slug}/interest: explicit antiforgery (400), role checks (403), local redirects only.</summary>
    public static IEndpointRouteBuilder MapLoop(this IEndpointRouteBuilder app)
    {
        app.MapPost("/me/inbox/{id:int}/reply", async (int id, HttpContext http, IAntiforgery antiforgery, BallpassDbContext db, TimeProvider time) =>
        {
            if (!await TokenValidAsync(antiforgery, http)) return Results.BadRequest();
            if (http.User.Identity?.IsAuthenticated != true) return Results.Redirect("/demo");
            if (!http.User.IsInRole("Athlete") || !int.TryParse(http.User.FindFirstValue("entity_id"), out var athleteId))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var form = await http.Request.ReadFormAsync();
            var reply = form["reply"].ToString() switch { "accept" => InterestReply.Accepted, "decline" => InterestReply.Declined, _ => InterestReply.None };
            if (reply == InterestReply.None) return Results.BadRequest();

            var interest = await db.Interests.FirstOrDefaultAsync(i => i.Id == id);
            if (interest is null) return Results.NotFound();
            if (interest.AthleteId != athleteId) return Results.StatusCode(StatusCodes.Status403Forbidden);

            // First reply wins; a repeated post changes nothing.
            if (interest.Reply == InterestReply.None)
            {
                var now = time.GetUtcNow();
                interest.Reply = reply;
                interest.RepliedAt = now;
                interest.SeenAt ??= now;
                await db.SaveChangesAsync();
            }
            return Results.Redirect("/me/inbox");
        }).DisableAntiforgery();

        app.MapPost("/demo/switch", async (string? to, HttpContext http, IAntiforgery antiforgery, BallpassDbContext db, IOptions<DemoOptions> demo) =>
        {
            if (!demo.Value.Enabled) return Results.NotFound();
            if (!await TokenValidAsync(antiforgery, http)) return Results.BadRequest();
            if (http.User.Identity?.IsAuthenticated != true) return Results.Redirect("/demo");

            ClaimsPrincipal principal;
            string home;
            if (to == "athlete")
            {
                if (!http.User.IsInRole("Coach") || !int.TryParse(http.User.FindFirstValue("entity_id"), out var coachId))
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                var athlete = await AthleteToShow(db, coachId);
                if (athlete is null) return Results.NotFound();
                principal = Principal(PersonaClaims.ForAthlete(athlete, coachId));
                home = "/me/inbox";
            }
            else if (to == "coach")
            {
                if (!http.User.IsInRole("Athlete") || !int.TryParse(http.User.FindFirstValue(PersonaClaims.SwitchFromCoach), out var coachId))
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                var coach = await db.Coaches.AsNoTracking().FirstOrDefaultAsync(c => c.Id == coachId && c.IsDemo);
                if (coach is null) return Results.NotFound();
                principal = Principal(PersonaClaims.ForCoach(coach));
                home = "/me/sent";
            }
            else return Results.BadRequest();

            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            return Results.Redirect(home);
        }).DisableAntiforgery();

        app.MapPost("/demo/reset", async (HttpContext http, IAntiforgery antiforgery, DemoDataSeeder seeder, IOptions<DemoOptions> demo) =>
        {
            if (!demo.Value.Enabled) return Results.NotFound();
            if (!await TokenValidAsync(antiforgery, http)) return Results.BadRequest();
            await seeder.ResetAsync();
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/?demo=reset");
            // The confirmation page (a routable component) lives on the same path and matches every HTTP method:
            // an explicit lower Order makes this POST endpoint win instead of throwing AmbiguousMatchException.
        }).DisableAntiforgery().WithOrder(-1);
        return app;
    }

    private static ClaimsPrincipal Principal(List<Claim> claims) =>
        new(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>The athlete of the coach's most recent interest; otherwise the best showcase athlete of the club's gender.</summary>
    private static async Task<Athlete?> AthleteToShow(BallpassDbContext db, int coachId)
    {
        var latest = await db.Interests.AsNoTracking().Where(i => i.CoachId == coachId && i.Athlete!.IsDemo)
            .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id).Select(i => i.Athlete).FirstOrDefaultAsync();
        if (latest is not null) return latest;

        var gender = await db.Coaches.Where(c => c.Id == coachId).Select(c => (Gender?)c.Club!.Gender).FirstOrDefaultAsync();
        if (gender is null) return null;
        return await db.Athletes.AsNoTracking().Where(a => a.IsDemo && a.Gender == gender)
            .OrderByDescending(a => db.Interests.Count(i => i.AthleteId == a.Id)).ThenByDescending(a => a.Stats.Count).ThenBy(a => a.Id)
            .FirstOrDefaultAsync();
    }

    private static async Task<bool> TokenValidAsync(IAntiforgery antiforgery, HttpContext http)
    {
        try { await antiforgery.ValidateRequestAsync(http); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
}
