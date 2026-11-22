using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ballpass.Web.Features.Engage;

/// <summary>
/// Hashtags: <c>#word</c> of Latin letters, Greek letters (monotonic and polytonic), digits and underscore, 2-40 characters, at least one letter,
/// not glued to a preceding word. Tags match case- and accent-insensitively (<c>ΠΡΟΠΟΝΗΣΗ</c> = <c>προπόνηση</c>); at most 3 per post.
/// </summary>
public static class PostTagParser
{
    public const int MaxTags = 3;
    public const int MaxLength = 40;

    private static readonly Regex Hash = new(@"(?<![\p{L}\p{N}_#&/@])#([A-Za-z0-9_Ͱ-Ͽἀ-῿]+)",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    /// <summary>Tags of the texts in order of appearance, deduplicated on the folded key. <c>Label</c> is the lower-cased text as typed.</summary>
    public static IReadOnlyList<(string Tag, string Label)> Parse(params string?[] texts)
    {
        var found = new List<(string Tag, string Label)>();
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text)) continue;
            foreach (Match m in Hash.Matches(text))
            {
                var word = m.Groups[1].Value;
                if (word.Length < 2 || word.Length > MaxLength || !word.Any(char.IsLetter)) continue;
                var key = Fold(word)!;
                if (found.Any(f => f.Tag == key)) continue;
                found.Add((key, word.ToLowerInvariant()));
                if (found.Count == MaxTags) return found;
            }
        }
        return found;
    }

    /// <summary>Matching key: no leading #, lower case, accents removed, final sigma folded to sigma. Null when nothing is left.</summary>
    public static string? Fold(string? raw)
    {
        var s = raw?.Trim().TrimStart('#').ToLowerInvariant();
        if (string.IsNullOrEmpty(s)) return null;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c == 'ς' ? 'σ' : c);
        }
        var folded = sb.ToString().Normalize(NormalizationForm.FormC);
        return folded.Length == 0 ? null : folded;
    }
}
