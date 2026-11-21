using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Ballpass.Web.Features.Find.Ai;

/// <summary>
/// The INlQueryParser the app uses. Rules always run (cheap, deterministic) and are the answer whenever the model is off,
/// unavailable, capped, slow or wrong. The model is only asked when: a key is configured, Ai:Enabled is not false (kill switch,
/// read live), the query is not an obvious injection, the query was not answered before, and the session cap is not reached.
/// No retries. Only the (length-capped) query text is sent; the demo data never leaves the app.
/// </summary>
public class GuardedNlQueryParser(RuleParser rules, INlQueryParser model, IOptionsMonitor<AiOptions> ai,
    IOptionsMonitor<AnthropicOptions> key, Func<string> sessionKey) : INlQueryParser
{
    private const int MaxCachedQueries = 500;

    private readonly ConcurrentDictionary<string, SearchFilters> cache = new();
    private readonly ConcurrentDictionary<string, int> callsPerSession = new();

    public async Task<SearchFilters> ParseAsync(string query, CancellationToken ct = default)
    {
        var options = ai.CurrentValue;
        var text = (query ?? "").Trim();
        if (text.Length > options.MaxQueryLength) text = text[..options.MaxQueryLength];
        if (text.Length == 0 || NlText.LooksLikeInjection(text)) return new SearchFilters();

        var fromRules = SearchFilterValidator.Validate(rules.Parse(text));
        if (!ModelAllowed(options)) return fromRules;

        var cacheKey = NlText.Normalize(text);
        if (cache.TryGetValue(cacheKey, out var cached)) return cached;
        if (!TryReserveCall(options)) return fromRules;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            var raw = await model.ParseAsync(text, cts.Token);
            var filters = SearchFilterValidator.Validate(raw) with { ParsedBy = ParsedBy.Ai };
            if (cache.Count >= MaxCachedQueries) cache.Clear();
            cache[cacheKey] = filters;
            return filters;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;   // the request itself was abandoned: do not turn it into an answer
        }
        catch (Exception)
        {
            // Timeout, refusal, max_tokens, bad JSON, rate limit / spend limit, network: rules answer, no retry.
            return fromRules;
        }
    }

    private bool ModelAllowed(AiOptions options)
    {
        var hasKey = !string.IsNullOrWhiteSpace(key.CurrentValue.ApiKey);
        return hasKey && (options.Enabled ?? true);
    }

    private bool TryReserveCall(AiOptions options)
    {
        var session = sessionKey();
        var allowed = true;
        callsPerSession.AddOrUpdate(session, 1, (_, used) =>
        {
            if (used >= options.MaxCallsPerSession) { allowed = false; return used; }
            return used + 1;
        });
        return allowed;
    }
}
