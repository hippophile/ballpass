using Ballpass.Web.Features.Evidence;

// Conventional namespace for AddXxx extensions: Program.cs needs no extra using line.
namespace Microsoft.Extensions.DependencyInjection;

public static class EvidenceSetup
{
    /// <summary>Evidence package services. The package has no endpoints (compare is a page), so there is no Map call.</summary>
    public static IServiceCollection AddEvidence(this IServiceCollection services)
    {
        services.AddOptions<EvidenceOptions>().BindConfiguration("Evidence");
        services.AddSingleton<IStatAggregator, StatAggregator>();
        services.AddSingleton<IBadgeService, BadgeService>();
        services.AddScoped<CohortService>();
        services.AddScoped<ICohortService>(sp => sp.GetRequiredService<CohortService>());
        services.AddScoped<ICohortRank>(sp => sp.GetRequiredService<CohortService>());
        return services;
    }
}
