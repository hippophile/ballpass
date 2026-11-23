using System.Globalization;

namespace Ballpass.Web.Components.Shared;

/// <summary>Ordinal numbers for resource placeholders: English "82nd" (1st, 2nd, 3rd, 11th-13th, 21st...); Greek the bare number
/// (the Greek resources add the "ο" themselves: "{0}ο εκατοστημόριο").</summary>
public static class Ordinal
{
    public static string Format(int n)
    {
        var number = n.ToString(CultureInfo.CurrentCulture);
        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "en") return number;
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return number + suffix;
    }
}
