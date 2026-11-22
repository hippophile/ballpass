using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Ballpass.Web.Components.Shared;

/// <summary>One relative-time wording for the whole app ("πριν από 5 λ."). Used by PostCard and the Sent page.</summary>
public static class RelativeTimeText
{
    public static string Format(IStringLocalizer<SharedResource> loc, DateTimeOffset at, DateTimeOffset now)
    {
        var age = now - at;
        if (age < TimeSpan.FromMinutes(1)) return loc["Time_Now"].Value;
        if (age < TimeSpan.FromHours(1)) return loc["Time_Minutes", (int)age.TotalMinutes].Value;
        if (age < TimeSpan.FromDays(1)) return loc["Time_Hours", (int)age.TotalHours].Value;
        if (age < TimeSpan.FromDays(30)) return loc["Time_Days", (int)age.TotalDays].Value;
        return at.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
    }
}
