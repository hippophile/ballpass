using System.Globalization;

namespace Ballpass.Web.Features.Find;

/// <summary>
/// Forgiving readers for query-string values on /search and /match. A hostile or mistyped value (letters in a number, "maybe" for a flag,
/// a NUL byte, a number past int range) is treated as absent: the page renders with that filter off instead of failing.
/// </summary>
public static class LenientQuery
{
    /// <summary>The value as an integer within [min, max]; null when missing, not a number or out of range.</summary>
    public static int? Int(string? value, int min, int max) =>
        int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max ? n : null;

    /// <summary>True only for "true", "1" or "on" (what a checkbox sends); everything else, including "maybe", is false.</summary>
    public static bool Flag(string? value) => value?.Trim().ToLowerInvariant() is "true" or "1" or "on";

    /// <summary>Trimmed text without control characters (NUL included; Postgres rejects it), cut to <paramref name="max"/>; null when nothing is left.</summary>
    public static string? Text(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length > max) clean = clean[..max].TrimEnd();
        return clean.Length == 0 ? null : clean;
    }
}
