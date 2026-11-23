using System.Globalization;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;

namespace Ballpass.Web.Components.Shared;

/// <summary>Seeded text in the UI language: English shows the English column when present, otherwise the original (runtime content has no
/// English column and shows as typed). Greek always shows the original.</summary>
public static class DisplayText
{
    public static bool IsGreek => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el";

    public static string Pick(string el, string? en) => IsGreek || string.IsNullOrEmpty(en) ? el : en;

    public static string? PickOrNull(string? el, string? en) => IsGreek || string.IsNullOrEmpty(en) ? el : en;

    public static string? Club(Club? club) => club is null ? null : Pick(club.Name, club.NameEn);

    public static string? Club(string? name, string? nameEn) => PickOrNull(name, nameEn);

    /// <summary>City as stored (Greek) in Greek, its English exonym otherwise.</summary>
    public static string City(string city) => IsGreek ? city : GreekNames.Cities.FirstOrDefault(c => c.Name == city)?.NameLatin ?? city;
}
