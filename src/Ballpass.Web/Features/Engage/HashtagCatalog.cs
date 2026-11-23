using System.Globalization;

namespace Ballpass.Web.Features.Engage;

/// <summary>
/// Hashtags of the seed in both languages. A tag is stored once, under its canonical key (the folded Greek tag, so Greek pages are
/// unchanged); the label follows the UI language, and the English alias (typed or in <c>/feed?tag=</c>) resolves to the same key, so
/// trending counts are the same in both languages.
/// </summary>
public static class HashtagCatalog
{
    private sealed record Entry(string Key, string El, string En);

    private static readonly Entry[] Entries =
    [
        new("ψαχνωομαδα", "ψάχνωομάδα", "lookingforteam"),
        new("tryouts", "tryouts", "tryouts"),
        new("δοκιμαστικη", "δοκιμαστική", "tryoutday"),
        new("προπονηση", "προπόνηση", "training"),
        new("ομαδικοτητα", "ομαδικότητα", "teamwork"),
        new("μπασκετ", "μπάσκετ", "basketball"),
        new("highlights", "highlights", "highlights"),
        new("καλαθι", "καλάθι", "buckets"),
        new("στατιστικα", "στατιστικά", "stats"),
        new("επιτυχια", "επιτυχία", "success"),
    ];

    private static readonly Dictionary<string, Entry> ByKey = Entries.ToDictionary(e => e.Key);
    private static readonly Dictionary<string, string> EnglishAlias = Entries.ToDictionary(e => e.En, e => e.Key);

    /// <summary>Canonical key of a typed tag (Greek or English alias): the folded text, mapped to the catalog key when it is a known alias.</summary>
    public static string? Key(string? raw) =>
        PostTagParser.Fold(raw) is { } folded ? EnglishAlias.GetValueOrDefault(folded, folded) : null;

    /// <summary>Label to show for a key in the current UI language; unknown keys keep the label as typed.</summary>
    public static string Label(string key, string typed) => ByKey.TryGetValue(key, out var e)
        ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? e.El : e.En
        : typed;

    /// <summary>English label of a catalog key (seeders); null for unknown keys.</summary>
    public static string? English(string key) => ByKey.GetValueOrDefault(key)?.En;
}
