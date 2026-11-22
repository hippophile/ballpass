using System.Globalization;

namespace Ballpass.Web.Features.Media;

/// <summary>Colours of a jersey avatar: fill, trim, and the colour of the number/initials on top of the fill.</summary>
public record JerseyPalette(string Fill, string Trim, string Text);

public static class JerseyStyle
{
    private const string Dark = "#0B0F14", Light = "#FFFFFF";

    // Fill colours with a readable number colour (contrast >= 4.5, asserted by MediaTests).
    private static readonly JerseyPalette[] Clubs =
    [
        new("#C8102E", Light, Light), new("#1F4FA3", Light, Light), new("#0E7C4A", Light, Light), new("#6A2C91", Light, Light),
        new("#F2B705", Dark, Dark), new("#E8590C", Dark, Dark), new("#0B7285", Light, Light), new("#212B3A", Light, Light),
        new("#C2255C", Light, Light), new("#E9ECEF", Dark, Dark),
    ];

    /// <summary>No club: a neutral slate that no club uses.</summary>
    public static readonly JerseyPalette Neutral = new("#5C6E8A", Light, Light);

    /// <summary>Deterministic 0-99 from an id (37 is coprime with 100, so 100 consecutive ids give 100 different numbers).</summary>
    public static int Number(int id) => (int)((Math.Abs((long)id) * 37 + 13) % 100);

    public static JerseyPalette Palette(string? club)
    {
        if (string.IsNullOrWhiteSpace(club)) return Neutral;
        uint hash = 2166136261;                                  // FNV-1a: stable across runs (string.GetHashCode is not)
        foreach (var ch in club.Trim()) hash = (hash ^ ch) * 16777619;
        return Clubs[hash % (uint)Clubs.Length];
    }

    /// <summary>WCAG contrast ratio of two #RRGGBB colours.</summary>
    public static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int i)
        {
            var c = int.Parse(hex.AsSpan(1 + 2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(1) + 0.0722 * Channel(2);
    }
}
