using System.Globalization;
using System.Resources;
using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Share;

/// <summary>Fixed-Greek strings for the card (the PNG is shared as one image, crawlers send no culture cookie).</summary>
internal static class ShareText
{
    private static readonly ResourceManager Share = new("Ballpass.Web.Resources.ShareResource", typeof(ShareResource).Assembly);
    private static readonly ResourceManager Shared = new("Ballpass.Web.Resources.SharedResource", typeof(SharedResource).Assembly);
    public static readonly CultureInfo El = new("el");

    public static string Greek(string key) => Share.GetString(key, El) ?? key;
    public static string GreekShared(string key) => Shared.GetString(key, El) ?? key;
}

public static class OgCardModelFactory
{
    // flow-release O1: no "DEMO". The card travels without the site footer, so its bands keep the one quiet disclosure.
    public const string WatermarkText = "ΦΑΝΤΑΣΤΙΚΟ ΠΡΟΣΩΠΟ · FICTIONAL PERSON";

    /// <summary>Needs <c>CurrentClub</c> and <c>Stats</c> loaded. The key line is the athlete's latest season.</summary>
    public static OgCardModel Build(Athlete athlete, int currentYear)
    {
        var name = $"{athlete.FirstName} {athlete.LastName}";
        var subtitle = string.Join(" · ",
            ShareText.GreekShared($"Position_{athlete.Position}"),
            $"{athlete.HeightCm} {ShareText.GreekShared("Profile_Cm")}",
            $"{currentYear - athlete.BirthYear} {ShareText.GreekShared("Profile_Years")}");
        var club = athlete.CurrentClub is null
            ? ShareText.Greek("Card_NoClub") + (athlete.LookingForTeam ? " · " + ShareText.Greek("Card_Looking") : "")   // follows the profile flag; the hash (cache key) covers this line
            : athlete.CurrentClub.Name;

        var line = athlete.Stats.OrderByDescending(s => s.Season, StringComparer.Ordinal).ThenByDescending(s => s.Games).FirstOrDefault();
        if (line is null)
            return new OgCardModel(athlete.Slug, name, subtitle, club, ShareText.Greek("Card_NoStats"), [], null, "", WatermarkText);

        var sourceLabel = ShareText.GreekShared($"Source_{line.Source}");
        return new OgCardModel(athlete.Slug, name, subtitle, club, $"{line.Season} · {sourceLabel}",
        [
            new CardStat(ShareText.Greek("Card_Points"), Num(line.Points)),
            new CardStat(ShareText.Greek("Card_Rebounds"), Num(line.Rebounds)),
            new CardStat(ShareText.Greek("Card_Assists"), Num(line.Assists)),
        ], line.Source, sourceLabel, WatermarkText);
    }

    private static string Num(decimal v) => v.ToString("0.0", ShareText.El);
}
