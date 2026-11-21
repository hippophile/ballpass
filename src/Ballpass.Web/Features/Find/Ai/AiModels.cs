namespace Ballpass.Web.Features.Find.Ai;

public enum ParsedBy { Rules, Ai }

/// <summary>Filters understood by /search (same names as its query string). Unsupported holds asks that match no field.</summary>
public record SearchFilters
{
    public string? Gender { get; init; }
    public IReadOnlyList<string> Positions { get; init; } = [];
    public int? MinHeight { get; init; }
    public int? MaxHeight { get; init; }
    public int? MinAge { get; init; }
    public int? MaxAge { get; init; }
    public string? Region { get; init; }
    public string? League { get; init; }
    public bool Looking { get; init; }
    public string? Q { get; init; }
    public IReadOnlyList<string> Unsupported { get; init; } = [];
    public ParsedBy ParsedBy { get; init; }

    // Value equality over the list members too (a record compares list references by default).
    public virtual bool Equals(SearchFilters? other) =>
        other is not null
        && Gender == other.Gender && MinHeight == other.MinHeight && MaxHeight == other.MaxHeight
        && MinAge == other.MinAge && MaxAge == other.MaxAge && Region == other.Region && League == other.League
        && Looking == other.Looking && Q == other.Q && ParsedBy == other.ParsedBy
        && Positions.SequenceEqual(other.Positions) && Unsupported.SequenceEqual(other.Unsupported);

    public override int GetHashCode() =>
        HashCode.Combine(Gender, MinHeight, MaxHeight, MinAge, MaxAge, Region, League, HashCode.Combine(Looking, Q, ParsedBy, Positions.Count, Unsupported.Count));
}

public interface INlQueryParser
{
    Task<SearchFilters> ParseAsync(string query, CancellationToken ct = default);
}

/// <summary>Thrown by the model parser for any outcome the guard must answer with the rule parser (refusal, bad JSON, API error).</summary>
public class AiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Reply of one model call, already reduced to what the parser needs.</summary>
public record ClaudeReply(string? StopReason, string? Text);

/// <summary>Thin seam over the Anthropic SDK so tests never reach the network.</summary>
public interface IClaudeClient
{
    Task<ClaudeReply> CompleteAsync(string query, CancellationToken ct);
}

/// <summary>Bound from "Ai". Enabled: null = on only if a key is configured; false = kill switch.</summary>
public class AiOptions
{
    public const string Section = "Ai";

    public bool? Enabled { get; set; }
    public string Model { get; set; } = "claude-opus-5-5";
    public double TimeoutSeconds { get; set; } = 4;
    public int MaxCallsPerSession { get; set; } = 20;
    public int MaxQueryLength { get; set; } = 200;
}

/// <summary>Bound from "Anthropic". The key comes ONLY from user-secrets or env Anthropic__ApiKey.</summary>
public class AnthropicOptions
{
    public const string Section = "Anthropic";

    public string? ApiKey { get; set; }
}
