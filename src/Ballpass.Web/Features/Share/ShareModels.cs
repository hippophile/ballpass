using System.Security.Cryptography;
using System.Text;
using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Share;

public record CardStat(string Label, string Value);

/// <summary>Everything the OG card draws. <see cref="Hash"/> changes whenever any drawn content changes.</summary>
public sealed record OgCardModel(
    string Slug, string Name, string Subtitle, string ClubLine, string SeasonLine,
    IReadOnlyList<CardStat> Stats, StatSource? Source, string SourceLabel, string WatermarkText)
{
    /// <summary>Bump when the drawing code changes so cached PNGs are re-rendered.</summary>
    public const string DesignVersion = "og-3";

    public string Hash
    {
        get
        {
            var sb = new StringBuilder(DesignVersion).Append('|')
                .Append(Slug).Append('|').Append(Name).Append('|').Append(Subtitle).Append('|').Append(ClubLine).Append('|')
                .Append(SeasonLine).Append('|').Append(Source).Append('|').Append(SourceLabel).Append('|').Append(WatermarkText);
            foreach (var s in Stats) sb.Append('|').Append(s.Label).Append('=').Append(s.Value);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16].ToLowerInvariant();
        }
    }
}

public interface IOgCardRenderer
{
    /// <summary>Returns a 1200x630 PNG.</summary>
    byte[] Render(OgCardModel model);
}
