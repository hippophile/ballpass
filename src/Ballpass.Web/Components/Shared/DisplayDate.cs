using System.Globalization;

namespace Ballpass.Web.Components.Shared;

/// <summary>One wording for display dates per culture. English: "Nov 22, 2026", "Sunday, Nov 1", "Nov 11". Greek: the formats the pages
/// used before (unchanged output).</summary>
public static class DisplayDate
{
    private static bool IsGreek => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el";

    /// <summary>Full date: "Nov 22, 2026" / "22 Νοε 2026". <paramref name="greekFormat"/> keeps a page's own Greek format.</summary>
    public static string Date(DateTime d, string greekFormat = "d MMM yyyy") =>
        d.ToString(IsGreek ? greekFormat : "MMM d, yyyy", CultureInfo.CurrentCulture);

    public static string Date(DateOnly d) => Date(d.ToDateTime(TimeOnly.MinValue));

    /// <summary>Day separator: "Sunday, Nov 1" / "Κυριακή 1 Νοεμβρίου"; the year only when it differs from <paramref name="currentYear"/>.</summary>
    public static string Day(DateTime d, int currentYear)
    {
        var sameYear = d.Year == currentYear;
        var format = IsGreek ? sameYear ? "dddd d MMMM" : "dddd d MMMM yyyy" : sameYear ? "dddd, MMM d" : "dddd, MMM d, yyyy";
        var text = d.ToString(format, CultureInfo.CurrentCulture);
        return char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
    }

    /// <summary>Month and day: "Nov 11" / "11 Νοε".</summary>
    public static string MonthDay(DateTime d) => d.ToString(IsGreek ? "d MMM" : "MMM d", CultureInfo.CurrentCulture);
}
