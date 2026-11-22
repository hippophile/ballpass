using System.Security.Claims;
using Ballpass.Web.Features.Find.Ai;
using Ballpass.Web.Features.Find.Fit;
using Microsoft.Extensions.Options;

namespace Ballpass.Web.Features.Find;

/// <summary>Services and endpoints of the Find package (Match page + natural-language search). Program.cs calls exactly these two.</summary>
public static class FindSetup
{
    public static IServiceCollection AddFind(this IServiceCollection services)
    {
        services.AddOptions<FitOptions>().BindConfiguration(FitOptions.Section);
        services.AddOptions<AiOptions>().BindConfiguration(AiOptions.Section);
        services.AddOptions<AnthropicOptions>().BindConfiguration(AnthropicOptions.Section);   // ApiKey: user-secrets / env Anthropic__ApiKey only

        services.AddHttpContextAccessor();
        services.AddScoped(sp => new FitScorer(sp.GetRequiredService<IOptions<FitOptions>>().Value));

        services.AddSingleton<RuleParser>();
        services.AddSingleton<IClaudeClient, AnthropicClaudeClient>();
        services.AddSingleton(sp => new ClaudeParser(sp.GetRequiredService<IClaudeClient>()));
        services.AddSingleton<INlQueryParser>(sp =>
        {
            var accessor = sp.GetRequiredService<IHttpContextAccessor>();
            return new GuardedNlQueryParser(
                sp.GetRequiredService<RuleParser>(),
                sp.GetRequiredService<ClaudeParser>(),
                sp.GetRequiredService<IOptionsMonitor<AiOptions>>(),
                sp.GetRequiredService<IOptionsMonitor<AnthropicOptions>>(),
                () => SessionKey(accessor.HttpContext),
                sp.GetService<ILogger<GuardedNlQueryParser>>());
        });
        return services;
    }

    public static IEndpointRouteBuilder MapFind(this IEndpointRouteBuilder app)
    {
        // GET so the natural-language box works in static SSR with no JavaScript: parse, then redirect to a normal filter URL.
        app.MapGet("/find/ai", async (string? q, string? to, INlQueryParser parser, HttpContext http) =>
        {
            var filters = await parser.ParseAsync(q ?? "", http.RequestAborted);
            return Results.Redirect(FindRedirect.Build(filters, to == "match" ? "match" : "search", q));
        }).RequireAuthorization("CoachOnly");
        return app;
    }

    /// <summary>"Session" for the call cap = the signed-in demo persona (one cap per coach), else the client address.</summary>
    private static string SessionKey(HttpContext? http) =>
        http?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http?.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
}
