namespace Ballpass.Web.Features.Find.Ai;

/// <summary>Builds the GET redirect target for parsed filters (static SSR: the page reads the query string, no JavaScript).</summary>
public static class FindRedirect
{
    public const int MaxEchoedQuery = 200;

    /// <param name="target">"match" or anything else (= search); never an arbitrary URL.</param>
    public static string Build(SearchFilters f, string target, string? originalQuery)
    {
        var isMatch = target == "match";
        var parts = new List<string>();
        void Add(string name, object? value)
        {
            if (value?.ToString() is { Length: > 0 } s) parts.Add($"{name}={Uri.EscapeDataString(s)}");
        }

        Add("gender", f.Gender);
        foreach (var p in f.Positions) Add("position", p);
        Add("minHeight", f.MinHeight);
        Add("maxHeight", f.MaxHeight);
        Add("minAge", f.MinAge);
        Add("maxAge", f.MaxAge);
        Add("region", f.Region);
        Add("league", f.League);
        if (!isMatch)
        {
            Add("looking", f.Looking ? "true" : null);
            Add("q", f.Q);
        }
        foreach (var u in f.Unsupported) Add("unsupported", u);
        Add("parsed", f.ParsedBy == ParsedBy.Ai ? "ai" : "rules");
        var echoed = originalQuery?.Trim();
        if (echoed is { Length: > MaxEchoedQuery }) echoed = echoed[..MaxEchoedQuery];
        Add("nlq", echoed);

        return (isMatch ? "/match?" : "/search?") + string.Join("&", parts);
    }
}
