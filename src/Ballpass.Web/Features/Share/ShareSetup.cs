using Ballpass.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Ballpass.Web.Features.Share;

public static class ShareSetup
{
    public static IServiceCollection AddShare(this IServiceCollection services)
    {
        services.AddSingleton<IOgCardRenderer, OgCardRenderer>();
        services.AddSingleton<OgCardService>();
        return services;
    }

    /// <summary>
    /// GET /p/{slug}/og.png: anonymous (crawlers send no cookie). The ETag is the content hash, so a conditional request is
    /// answered with 304 before any rendering; otherwise the card comes from the cache or is rendered (max 2 at once).
    /// </summary>
    public static WebApplication MapShare(this WebApplication app)
    {
        app.MapGet("/p/{slug}/og.png", async (string slug, BallpassDbContext db, HttpContext http, OgCardService cards, TimeProvider time, CancellationToken ct) =>
        {
            var athlete = await db.Athletes.AsNoTracking()
                .Include(a => a.CurrentClub).Include(a => a.Stats)
                .AsSplitQuery()
                .FirstOrDefaultAsync(a => a.Slug == slug && a.IsDemo, ct);
            if (athlete is null) return Results.NotFound();

            var model = OgCardModelFactory.Build(athlete, time.GetUtcNow().Year);
            var etag = $"\"{model.Hash}\"";
            http.Response.Headers.ETag = etag;
            http.Response.Headers.CacheControl = "public, max-age=300";

            if (Matches(http.Request.Headers.IfNoneMatch, etag)) return Results.StatusCode(StatusCodes.Status304NotModified);

            var result = await cards.GetAsync(model, ct);
            http.Response.Headers["X-Card-Cache"] = result.Source;
            if (result.Source == "render") http.Response.Headers["X-Card-Render-Ms"] = result.RenderMs.ToString();
            return Results.File(result.Png, "image/png");
        }).AllowAnonymous();
        return app;
    }

    private static bool Matches(StringValues header, string etag) =>
        header.Any(h => h is not null && h.Split(',').Select(t => t.Trim()).Any(t => t == "*" || t == etag || t == "W/" + etag));
}
