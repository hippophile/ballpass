using Ballpass.Web.Features.Media;

// Root namespace on purpose: Program.cs already imports Ballpass.Web, so it needs no extra using for AddMedia.
namespace Ballpass.Web;

public static class MediaSetup
{
    /// <summary>Media is read-only for visitors (seeded demo content); no endpoints, so there is no MapMedia.</summary>
    public static IServiceCollection AddMedia(this IServiceCollection services)
    {
        services.AddScoped<IMediaService, MediaService>();
        return services;
    }
}
