namespace Ballpass.Web.Features.Share;

public static class GreekGenitive
{
    /// <summary>
    /// Genitive of a Greek female first name ("Ελένη" -> "Ελένης"), or null when no safe rule applies
    /// (names in -ώ/-ω and anything unrecognised). Callers fall back to a wording that needs no genitive.
    /// Feminine -α, -η, -ή take -ς and keep the accent: Μαρία -> Μαρίας, Ζωή -> Ζωής.
    /// </summary>
    public static string? FemaleFirstName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return name[^1] is 'α' or 'ά' or 'η' or 'ή' ? name + "ς" : null;
    }
}
