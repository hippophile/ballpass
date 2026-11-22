using System.Text.RegularExpressions;
using Ballpass.Web.Features.Engage;
using Ballpass.Web.Features.Feed;
using Microsoft.AspNetCore.Antiforgery;

// Root namespace on purpose: Program.cs already imports Ballpass.Web, so it needs no extra using for AddEngage/MapEngage.
namespace Ballpass.Web;

public static class EngageSetup
{
    public static IServiceCollection AddEngage(this IServiceCollection services)
    {
        services.AddOptions<EngageOptions>().BindConfiguration("Engage");
        services.AddHttpContextAccessor();
        services.AddScoped<IEngageService, EngageService>();
        services.AddScoped<EngageCache>();
        return services;
    }

    /// <summary>Plain-form POST endpoints. Same rules as /feed/*: explicit antiforgery (400), signed-in and ownership checks (403), local redirects only.
    /// Feedback goes back as <c>?cs=word</c> next to the post anchor.</summary>
    public static IEndpointRouteBuilder MapEngage(this IEndpointRouteBuilder app)
    {
        Map(app, "/feed/comment", async (form, viewer, svc) =>
        {
            if (!viewer.SignedIn) return Forbidden;
            if (!int.TryParse(form["postId"], out var postId)) return Results.BadRequest();
            return await svc.AddCommentAsync(viewer, postId, form["text"]) switch
            {
                CommentOutcome.Posted => new Done("posted", postId),
                CommentOutcome.RateLimited => new Done("rate", postId),
                CommentOutcome.TooLong => new Done("too-long", postId),
                CommentOutcome.Invalid => new Done("invalid", postId),
                CommentOutcome.NotFound => Results.NotFound(),
                _ => Forbidden, // forbidden, minor, blocked
            };
        });
        Map(app, "/feed/comment/delete", async (form, viewer, svc) => await Moderate(form, viewer, id => svc.DeleteCommentAsync(viewer, id), "deleted"));
        Map(app, "/feed/comment/hide", async (form, viewer, svc) => await Moderate(form, viewer, id => svc.HideCommentAsync(viewer, id), "hidden"));
        Map(app, "/feed/comment/report", async (form, viewer, svc) => await Moderate(form, viewer, id => svc.ReportCommentAsync(viewer, id, form["reason"]), "reported"));
        return app;
    }

    private record Done(string Status, int PostId);

    private static readonly IResult Forbidden = Results.StatusCode(StatusCodes.Status403Forbidden);

    private static async Task<object> Moderate(IFormCollection form, FeedViewer viewer, Func<int, Task<ModOutcome>> action, string status)
    {
        if (!viewer.SignedIn) return Forbidden;
        if (!int.TryParse(form["commentId"], out var commentId)) return Results.BadRequest();
        return await action(commentId) switch
        {
            ModOutcome.Ok => status,
            ModOutcome.NotFound => Results.NotFound(),
            ModOutcome.Invalid => "invalid",
            _ => Forbidden,
        };
    }

    private static void Map(IEndpointRouteBuilder app, string path, Func<IFormCollection, FeedViewer, IEngageService, Task<object>> handler) =>
        app.MapPost(path, async (HttpContext http, IAntiforgery antiforgery, IEngageService svc) =>
        {
            if (!await antiforgery.IsValidAsync(http)) return Results.BadRequest();
            if (!http.Request.HasFormContentType) return Results.BadRequest();

            var form = await http.Request.ReadFormAsync();
            var result = await handler(form, FeedViewer.From(http.User), svc);
            if (result is IResult r) return r;
            var returnUrl = form["returnUrl"].ToString();
            var (status, postId) = result is Done d ? (d.Status, d.PostId) : ((string)result, (int?)null);
            var back = WithParam(LocalRedirect.IsSafe(returnUrl) ? returnUrl : "/feed", "cs", status);
            // Back to the post that was commented on, even when the form carried no anchor.
            return Results.Redirect(postId is { } id && !back.Contains('#') ? $"{back}#post-{id}" : back);
        }).DisableAntiforgery();

    /// <summary>Sets one query parameter and keeps the #post-N fragment.</summary>
    internal static string WithParam(string url, string name, string value)
    {
        var hash = url.IndexOf('#');
        var fragment = hash >= 0 ? url[hash..] : "";
        var path = hash >= 0 ? url[..hash] : url;
        path = Regex.Replace(path, $@"([?&]){name}=[^&]*&?", "$1").TrimEnd('?', '&');
        return $"{path}{(path.Contains('?') ? '&' : '?')}{name}={value}{fragment}";
    }
}
