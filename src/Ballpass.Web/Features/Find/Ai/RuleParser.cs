using System.Globalization;
using System.Text.RegularExpressions;

namespace Ballpass.Web.Features.Find.Ai;

/// <summary>
/// Keyword/pattern parser for Greek and English scouting queries. Always available, no model, no network.
/// Matches on accent-free lower-case text (see <see cref="NlText.Normalize"/>). Anything it cannot map to a field stays
/// out of the filters; asks that clearly have no field (handedness, salary...) become "unsupported" chips.
/// </summary>
public partial class RuleParser : INlQueryParser
{
    private const string L = @"[\p{L}\p{N}]";

    private static Regex Rx(string pattern) =>
        new($@"(?<!{L})(?:{pattern})(?!{L})", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Female = Rx(@"γυναικ\w*|κοριτσ\w*|αθλητρι\w*|παιχτρι\w*|women|woman|womens|female|girls?|ladies");
    private static readonly Regex Male = Rx(@"ανδρ\w*|αγορι\w*|men|man|mens|male|boys?");

    // Specific positions first; they are blanked out before the generic "guard" / "forward" words are tried.
    private static readonly (string Code, Regex Rx)[] SpecificPositions =
    [
        ("PG", Rx(@"point\s*guard|ποιντ\s*γκαρντ|πλει\s*μεικερ|play\s*maker|pg")),
        ("SG", Rx(@"shooting\s*guard|σουτινγκ\s*γκαρντ|sg")),
        ("SF", Rx(@"small\s*forward|σμολ\s*φορ\p{L}{2,8}ντ|sf")),
        ("PF", Rx(@"power\s*forward|παουερ\s*φορ\p{L}{2,8}ντ|pf")),
    ];
    private static readonly Regex Guard = Rx(@"guards?|γκαρντ");
    private static readonly Regex Forward = Rx(@"forwards?|φορ\p{L}{2,8}ντ");
    private static readonly Regex Center = Rx(@"centers?|centres?|σεντερ\w*|pivot");

    private static readonly (string Region, Regex Rx)[] Regions =
    [
        ("East Macedonia and Thrace", Rx(@"ανατολικ\w*\s+μακεδονι\w*(?:\s+και\s+θρακ\w*)?|east\w*\s+macedonia|θρακ\w*|thrace|κομοτην\w*|komotini")),
        ("Central Macedonia", Rx(@"κεντρικ\w*\s+μακεδονι\w*|central\s+macedonia|θεσσαλονικ\w*|thessaloniki|salonica|σερρ\w*|serres")),
        ("Western Greece", Rx(@"δυτικ\w*\s+ελλαδ\w*|western\s+greece|πατρα\w*|patras|patra|αγρινι\w*|agrinio")),
        ("Crete", Rx(@"κρητ\w*|crete|ηρακλει\w*|heraklion|irakleio|χανι\w*|chania")),
        ("Thessaly", Rx(@"θεσσαλι\w*|thessaly|λαρισ\w*|larisa|βολο\w*|volos")),
        ("Epirus", Rx(@"ηπειρ\w*|epirus|ιωαννιν\w*|ioannina")),
        ("South Aegean", Rx(@"νοτι\w*\s+αιγαι\w*|south\s+aegean|ροδο\w*|rhodes|rodos")),
        ("Attica", Rx(@"αττικ\w*|attica|αθην\w*|athens|athina|πειραι\w*|piraeus|peiraias|περιστερι\w*|peristeri")),
    ];

    private static readonly (string League, Regex Rx)[] Leagues =
    [
        ("A1", Rx(@"[αa]\s?1")),
        ("A2", Rx(@"[αa]\s?2")),
        ("B", Rx(@"[βb][΄'’ʹ]?\s*(?:εθνικη|league|division|κατηγορια)")),
        ("Regional", Rx(@"τοπικ\w*|regional")),
    ];

    private static readonly Regex Looking = Rx(
        @"ψαχν\w*\s+(?:για\s+)?(?:ομαδ\w*|συλλογ\w*)|χωρισ\s+ομαδα|ελευθερ\w*|free\s*agents?|looking\s+for\s+(?:a\s+)?(?:team|club)|without\s+(?:a\s+)?(?:team|club)|available|διαθεσιμ\w*|unsigned|no\s+team");

    private static readonly (string Key, Regex Rx)[] Unsupported =
    [
        ("handedness", Rx(@"αριστερ\w*|δεξιοχειρ\w*|δεξι[αά]\s+χερι|left[\s-]?hand\w*|right[\s-]?hand\w*|lefty|southpaw")),
        ("salary", Rx(@"μισθ\w*|salary|wages?|ευρω|euros?|eur|€|αμοιβ\w*")),
        ("nationality", Rx(@"διαβατηρι\w*|passport|ιθαγενει\w*|nationality|υπηκο\w*|citizen\w*|ξεν[οη]\w*|foreign\w*")),
        ("stats", Rx(@"ριμπ\w*|rebound\w*|σκορερ\w*|scorers?|assists?|ασσιστ\w*|πασες|steals?|κλεψιματ\w*|μπλοκ\w*|blocks?")),
    ];

    private static readonly Regex NameAfterKeyword = new(
        @"(?<![\p{L}])(?:[οό]νομα|named|name)\s*:?\s+(\p{L}[\p{L}'\-]{1,30})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // One number expression: optional comparator, number (optionally a range), optional unit.
    private const string Num = @"\d{1,3}(?:[.,]\d{1,2})?";
    private const string LowerBound = @"ανω\s+των|ανω\s+του|ανω\s+απο|πανω\s+απο|πανω\s+των|τουλαχιστον|ελαχιστ\w*|ψηλοτερ\w*(?:\s+απο)?|μεγαλυτερ\w*(?:\s+απο)?|over|above|at\s+least|min(?:imum)?|taller\s+than|older\s+than|more\s+than|>=|>|\+";
    private const string UpperBound = @"κατω\s+των|κατω\s+του|κατω\s+απο|μεχρι|εως|το\s+πολυ|under|below|at\s+most|max(?:imum)?|up\s+to|shorter\s+than|younger\s+than|less\s+than|μικροτερ\w*(?:\s+απο)?|<=|<";
    private const string AgeUnit = @"ετων|ετη|χρονων|χρονα|χρονη|years?|yrs?|yo|y/o";
    private const string HeightUnit = @"εκατοστων|εκατοστα|εκ|cm|μετρα|meters?|metres?|μ|m";

    private static readonly Regex NumberExpression = new(
        $@"(?:(?<lb>(?<!{L})(?:{LowerBound})(?!{L})|(?<=\s|^)(?:>=|>|\+))|(?<ub>(?<!{L})(?:{UpperBound})(?!{L})|(?<=\s|^)(?:<=|<)))?\s*"
        + $@"(?<![\p{{L}}\p{{N}}.,])(?<n1>{Num})(?!\p{{N}})"
        + $@"(?:\s*(?:-|εως|μεχρι|to|ως)\s*(?<n2>{Num})(?!\p{{N}}))?"
        + $@"(?:\s*(?:(?<age>(?:{AgeUnit})(?!{L}))|(?<hgt>(?:{HeightUnit})(?!{L}))))?",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public Task<SearchFilters> ParseAsync(string query, CancellationToken ct = default) => Task.FromResult(Parse(query));

    public SearchFilters Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new SearchFilters();
        var original = query.Length > 300 ? query[..300] : query;
        if (NlText.LooksLikeInjection(original)) return new SearchFilters();

        string? q = null;
        var nameMatch = NameAfterKeyword.Match(original);
        if (nameMatch.Success)
        {
            q = nameMatch.Groups[1].Value;
            original = original.Remove(nameMatch.Index, nameMatch.Length);
        }

        var work = Normalize(original);
        var unsupported = new List<string>();

        string? region = null;
        var best = int.MaxValue;
        foreach (var (name, rx) in Regions)
        {
            var m = rx.Match(work);
            if (m.Success && m.Index < best) { best = m.Index; region = name; }
        }
        foreach (var (_, rx) in Regions) work = rx.Replace(work, " ");

        string? league = null;
        foreach (var (name, rx) in Leagues)
        {
            if (league is null && rx.IsMatch(work)) league = name;
            work = rx.Replace(work, " ");
        }

        var looking = Looking.IsMatch(work);
        work = Looking.Replace(work, " ");

        foreach (var (key, rx) in Unsupported)
        {
            if (rx.IsMatch(work)) unsupported.Add(key);
            work = rx.Replace(work, " ");
        }

        var (minH, maxH, minA, maxA) = Numbers(ref work);

        var female = Female.IsMatch(work);
        var male = Male.IsMatch(work);
        string? gender = female == male ? null : female ? "Female" : "Male";

        var positions = new List<string>();
        foreach (var (code, rx) in SpecificPositions)
        {
            if (rx.IsMatch(work)) positions.Add(code);
            work = rx.Replace(work, " ");
        }
        if (Guard.IsMatch(work)) positions.AddRange(["PG", "SG"]);
        if (Forward.IsMatch(work)) positions.AddRange(["SF", "PF"]);
        if (Center.IsMatch(work)) positions.Add("C");

        return new SearchFilters
        {
            Gender = gender,
            Positions = positions.Distinct().ToArray(),
            MinHeight = minH, MaxHeight = maxH, MinAge = minA, MaxAge = maxA,
            Region = region, League = league, Looking = looking, Q = q,
            Unsupported = unsupported.ToArray(),
            ParsedBy = ParsedBy.Rules,
        };
    }

    private static string Normalize(string text)
    {
        var n = NlText.Normalize(text);
        return Regex.Replace(n, @"[^\p{L}\p{N}.,\-<>=+€/]", " ");
    }

    private static (int? MinH, int? MaxH, int? MinA, int? MaxA) Numbers(ref string work)
    {
        int? minH = null, maxH = null, minA = null, maxA = null;

        foreach (Match m in NumberExpression.Matches(work))
        {
            var n1 = ParseNumber(m.Groups["n1"].Value);
            var hasUnitAge = m.Groups["age"].Success;
            var hasUnitHeight = m.Groups["hgt"].Success;
            var kind = Classify(n1, hasUnitAge, hasUnitHeight);
            if (kind == Kind.None) continue;

            var lo = kind == Kind.Height ? ToCm(n1) : (int)Math.Round(n1);
            if (m.Groups["n2"].Success)
            {
                var n2 = ParseNumber(m.Groups["n2"].Value);
                var hi = kind == Kind.Height ? ToCm(n2) : (int)Math.Round(n2);
                if (kind == Kind.Height) { minH ??= lo; maxH ??= hi; }
                else { minA ??= lo; maxA ??= hi; }
            }
            else if (m.Groups["ub"].Success)
            {
                if (kind == Kind.Height) maxH ??= lo; else maxA ??= lo;
            }
            else if (m.Groups["lb"].Success)
            {
                if (kind == Kind.Height) minH ??= lo; else minA ??= lo;
            }
            else if (kind == Kind.Height) minH ??= lo;   // a bare height means "about this tall or more"
            else { minA ??= lo; maxA ??= lo; }           // a bare age means exactly that age
        }

        work = NumberExpression.Replace(work, " ");
        return (minH, maxH, minA, maxA);
    }

    private enum Kind { None, Height, Age }

    private static Kind Classify(double value, bool ageUnit, bool heightUnit)
    {
        if (ageUnit) return Kind.Age;
        if (heightUnit) return value is >= 1.2 and <= 2.4 or >= 130 and <= 260 ? Kind.Height : Kind.None;
        if (value is >= 1.2 and <= 2.4) return Kind.Height;     // metres, e.g. 1.85
        if (value is >= 130 and <= 260) return Kind.Height;
        if (value is >= 14 and <= 80) return Kind.Age;
        return Kind.None;
    }

    private static double ParseNumber(string s) => double.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);

    private static int ToCm(double v) => v < 3 ? (int)Math.Round(v * 100) : (int)Math.Round(v);
}
