using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ballpass.Web.Features.Find.Ai;

public static partial class NlText
{
    /// <summary>Lower-case, accent-free (Greek tonos/dialytika), final sigma folded, whitespace collapsed. Used for matching and for the cache key.</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch == 'ς' || ch == 'Σ' ? 'σ' : char.ToLowerInvariant(ch));
        }
        return Whitespace().Replace(sb.ToString(), " ").Trim();
    }

    /// <summary>
    /// Cheap guard for the obvious "ignore your instructions" family (Greek + English). A hit means: no model call, empty filters.
    /// This is a courtesy filter, not the security boundary: the model output is schema-checked and validated anyway.
    /// </summary>
    public static bool LooksLikeInjection(string text) => Injection().IsMatch(Normalize(text));

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(
        @"(ignore|disregard|forget|override|bypass)\b.{0,40}\b(previous|prior|above|earlier|system|instructions?|prompts?|rules?)\b"
        + @"|system\s+prompt|developer\s+mode|jailbreak|you\s+are\s+now\b|reveal\b.{0,20}\b(prompt|instructions?)\b"
        + @"|(αγνο\p{L}*|ξεχασ\p{L}*|παρακαμψ\p{L}*)\b.{0,40}\b(οδηγι\p{L}*|εντολ\p{L}*|κανον\p{L}*)"
        + @"|προηγουμεν\p{L}*\s+(οδηγι|εντολ)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Injection();
}
