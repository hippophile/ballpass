using System.Text;
using Ballpass.Web.Demo;

namespace Ballpass.Web.Features.Find.Ai;

/// <summary>Single choke point for every parser's output (rules or model): enums, ranges, adult-only ages, free text.</summary>
public static class SearchFilterValidator
{
    public static readonly string[] Positions = ["PG", "SG", "SF", "PF", "C"];
    public static readonly string[] Leagues = ["A1", "A2", "B", "Regional"];
    public static readonly string[] Genders = ["Female", "Male"];
    public static readonly string[] Regions = GreekNames.Cities.Select(c => c.Region).Distinct().OrderBy(r => r).ToArray();

    /// <summary>Keys the model may put in `unsupported`. The UI has a Greek label (Unsupported_{key}) for every key in <see cref="UnsupportedKeys"/>.</summary>
    public static readonly string[] ModelUnsupportedKeys = ["handedness", "salary", "nationality", "stats", "other"];
    public static readonly string[] UnsupportedKeys = [.. ModelUnsupportedKeys, "minors"];

    private static readonly Dictionary<string, string> UnsupportedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["statistics"] = "stats", ["statistic"] = "stats", ["stat"] = "stats",
    };

    private const int MinHeightCm = 100, MaxHeightCm = 260, MinAdultAge = 18, MaxAgeYears = 80, MaxQLength = 80, MaxUnsupported = 5, MaxUnsupportedLength = 40;

    public static SearchFilters Validate(SearchFilters raw)
    {
        var unsupported = new List<string>();

        int? minHeight = InRange(raw.MinHeight, MinHeightCm, MaxHeightCm), maxHeight = InRange(raw.MaxHeight, MinHeightCm, MaxHeightCm);
        if (minHeight > maxHeight) (minHeight, maxHeight) = (maxHeight, minHeight);

        int? minAge = raw.MinAge is { } lo ? Math.Clamp(lo, MinAdultAge, MaxAgeYears) : null;
        int? maxAge = raw.MaxAge;
        if (maxAge < MinAdultAge)
        {
            // "Under 16" cannot be served: adults only. The lower bound becomes 18, the upper bound stays open (not "exactly 18"), and the coach is told.
            maxAge = null;
            minAge = MinAdultAge;
            unsupported.Add("minors");
        }
        else if (maxAge is { } hi) maxAge = Math.Min(hi, MaxAgeYears);
        if (minAge > maxAge) (minAge, maxAge) = (maxAge, minAge);

        foreach (var item in raw.Unsupported)
        {
            var clean = CleanText(item, MaxUnsupportedLength);
            if (clean.Length == 0) continue;
            // Only known keys leave the validator (each has a label in the UI); anything else is "other".
            var key = UnsupportedAliases.TryGetValue(clean, out var alias) ? alias
                : UnsupportedKeys.FirstOrDefault(k => k.Equals(clean, StringComparison.OrdinalIgnoreCase)) ?? "other";
            if (!unsupported.Contains(key)) unsupported.Add(key);
        }

        var q = CleanText(raw.Q, MaxQLength);

        return new SearchFilters
        {
            Gender = Canonical(raw.Gender, Genders),
            Positions = raw.Positions.Select(p => Canonical(p, Positions)).OfType<string>().Distinct().ToArray(),
            MinHeight = minHeight, MaxHeight = maxHeight,
            MinAge = minAge, MaxAge = maxAge,
            Region = Canonical(raw.Region, Regions),
            League = Canonical(raw.League, Leagues),
            Looking = raw.Looking,
            Q = q.Length == 0 || NlText.LooksLikeInjection(q) ? null : q,
            Unsupported = unsupported.Take(MaxUnsupported).ToArray(),
            ParsedBy = raw.ParsedBy,
        };
    }

    private static int? InRange(int? v, int min, int max) => v is { } x && x >= min && x <= max ? x : null;

    private static string? Canonical(string? value, string[] allowed) =>
        value is null ? null : allowed.FirstOrDefault(a => a.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Letters, digits, space and a few joiners only (no markup, no control characters), trimmed and length-capped.</summary>
    private static string CleanText(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder();
        foreach (var ch in text)
            if (char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '\'' or '.' or '_') sb.Append(ch);
        var clean = sb.ToString().Trim();
        return clean.Length > max ? clean[..max].TrimEnd() : clean;
    }
}
