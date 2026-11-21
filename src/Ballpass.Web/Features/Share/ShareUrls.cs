namespace Ballpass.Web.Features.Share;

public static class ShareUrls
{
    /// <summary>Absolute URL for <paramref name="path"/>: <paramref name="publicBaseUrl"/> when configured, else the request base URI.</summary>
    public static string Absolute(string? publicBaseUrl, string requestBaseUri, string path)
    {
        var baseUrl = string.IsNullOrWhiteSpace(publicBaseUrl) ? requestBaseUri : publicBaseUrl.Trim();
        return baseUrl.TrimEnd('/') + path;
    }
}
