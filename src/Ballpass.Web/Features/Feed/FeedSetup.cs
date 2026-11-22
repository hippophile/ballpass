using System.Globalization;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Features.Feed;
using Microsoft.AspNetCore.Antiforgery;

// Root namespace on purpose: Program.cs already imports Ballpass.Web, so it needs no extra using for AddFeed/MapFeed.
namespace Ballpass.Web;

public static class FeedSetup
{
    public static IServiceCollection AddFeed(this IServiceCollection services)
    {
        services.AddOptions<FeedOptions>().BindConfiguration("Feed");
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<IFollowService, FollowService>();
        return services;
    }

    /// <summary>Plain-form POST endpoints. Same rules as /p/{slug}/interest: explicit antiforgery (400), role checks (403), local redirects only.</summary>
    public static IEndpointRouteBuilder MapFeed(this IEndpointRouteBuilder app)
    {
        Map(app, "/feed/post", async (form, viewer, svc, _) =>
        {
            if (!viewer.SignedIn) return Forbidden;
            if (!Enum.TryParse<PostType>(form["type"], out var type) || !Enum.IsDefined(type)) return Results.BadRequest();
            var input = new NewPost(type)
            {
                Text = form["text"],
                Title = form["title"],
                LookingOn = form["lft"] != "off",
                Position = Enum.TryParse<Position>(form["position"], out var pos) && Enum.IsDefined(pos) ? pos : null,
                Region = form["region"],
                AvailableFrom = Date(form["availableFrom"]),
                EventDate = Date(form["eventDate"]),
                Place = form["place"],
                Positions = form["positions"],
                Deadline = Date(form["deadline"]),
            };
            return await svc.CreateAsync(viewer, input) switch
            {
                PostOutcome.Posted => "posted",
                PostOutcome.SwitchedOff => "lft-off",
                PostOutcome.RateLimited => "rate",
                PostOutcome.TooLong => "too-long",
                PostOutcome.Invalid => "invalid",
                _ => Forbidden,
            };
        });

        Map(app, "/feed/kudos", async (form, viewer, svc, _) =>
        {
            if (!viewer.SignedIn) return Forbidden;
            if (!int.TryParse(form["postId"], out var postId)) return Results.BadRequest();
            return await svc.SetKudosAsync(viewer, postId, form["on"] == "1") ? "kudos" : Results.NotFound();
        });

        Map(app, "/feed/report", async (form, viewer, svc, _) =>
            await Moderate(form, async postId => await svc.ReportAsync(viewer, postId, form["reason"]), "reported", viewer));
        Map(app, "/feed/hide", async (form, viewer, svc, _) =>
            await Moderate(form, async postId => await svc.HideAsync(viewer, postId), "hidden", viewer));
        Map(app, "/feed/block", async (form, viewer, svc, _) =>
            await Moderate(form, async postId => await svc.BlockAuthorAsync(viewer, postId), "blocked", viewer));

        Map(app, "/feed/follow", async (form, viewer, _, follows) =>
        {
            if (!viewer.SignedIn) return Forbidden;
            return await follows.FollowAsync(viewer.Key!, form["targetKey"].ToString()) ? "following" : "follow-refused";
        });
        Map(app, "/feed/unfollow", async (form, viewer, _, follows) =>
        {
            if (!viewer.SignedIn) return Forbidden;
            await follows.UnfollowAsync(viewer.Key!, form["targetKey"].ToString());
            return "unfollowed";
        });
        return app;
    }

    private static readonly IResult Forbidden = Results.StatusCode(StatusCodes.Status403Forbidden);

    private static DateOnly? Date(string? s) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static async Task<object> Moderate(IFormCollection form, Func<int, Task<ModOutcome>> action, string status, FeedViewer viewer)
    {
        if (!viewer.SignedIn) return Forbidden;
        if (!int.TryParse(form["postId"], out var postId)) return Results.BadRequest();
        return await action(postId) switch
        {
            ModOutcome.Ok => status,
            ModOutcome.NotFound => Results.NotFound(),
            ModOutcome.Invalid => "invalid",
            _ => Forbidden,
        };
    }

    /// <summary>Handler returns a status word (redirect back with ?status=word) or a ready IResult.</summary>
    private static void Map(IEndpointRouteBuilder app, string path,
        Func<IFormCollection, FeedViewer, IFeedService, IFollowService, Task<object>> handler) =>
        app.MapPost(path, async (HttpContext http, IAntiforgery antiforgery, IFeedService svc, IFollowService follows) =>
        {
            if (!await antiforgery.IsValidAsync(http)) return Results.BadRequest();
            if (!http.Request.HasFormContentType) return Results.BadRequest();

            var form = await http.Request.ReadFormAsync();
            var result = await handler(form, FeedViewer.From(http.User), svc, follows);
            if (result is IResult r) return r;
            var returnUrl = form["returnUrl"].ToString();
            return Results.Redirect(WithStatus(LocalRedirect.IsSafe(returnUrl) ? returnUrl : "/feed", (string)result));
        }).DisableAntiforgery();

    private static string WithStatus(string url, string status)
    {
        var hash = url.IndexOf('#');
        var fragment = hash >= 0 ? url[hash..] : ""; // #post-N anchor: the page returns to the same post
        url = Regex.Replace(hash >= 0 ? url[..hash] : url, @"([?&])status=[^&]*&?", "$1").TrimEnd('?', '&');
        return $"{url}{(url.Contains('?') ? '&' : '?')}status={status}{fragment}";
    }

    /// <summary>Shortlist seam: a feed failure must never break shortlisting.</summary>
    public static async Task TryFollowAsync(this IServiceProvider services, string followerKey, string targetKey)
    {
        try { await services.GetRequiredService<IFollowService>().FollowAsync(followerKey, targetKey); }
        catch (Exception e) { services.GetService<ILoggerFactory>()?.CreateLogger("Feed").LogWarning(e, "Follow after shortlist failed"); }
    }
}
