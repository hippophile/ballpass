using System.Net;
using System.Text.Json;
using Ballpass.Web.Features.Find.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-audit-fixes/specs/find/spec.md (client never retries, robust model call). No real API call anywhere.
public class AuditAiTests
{
    private sealed class Monitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    // ---------- 3. MaxRetries = 0 (Clara measured 3 requests for one 500 with the SDK default) ----------

    /// <summary>Local fake of the API: answers every request with HTTP 500 and counts them.</summary>
    private sealed class FakeApi : IDisposable
    {
        private readonly HttpListener listener = new();
        private readonly Task loop;
        public int Requests;
        public string BaseUrl { get; }

        public FakeApi()
        {
            var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();
            BaseUrl = $"http://127.0.0.1:{port}";
            listener.Prefixes.Add(BaseUrl + "/");
            listener.Start();
            loop = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync(); }
                    catch (Exception) { return; }
                    Interlocked.Increment(ref Requests);
                    var body = """{"type":"error","error":{"type":"api_error","message":"boom"}}"""u8.ToArray();
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.OutputStream.WriteAsync(body);
                    ctx.Response.Close();
                }
            });
        }

        public void Dispose() { listener.Stop(); listener.Close(); }
    }

    [Fact]
    public async Task One_server_error_is_exactly_one_request_and_an_unavailable_exception_with_the_status()
    {
        using var api = new FakeApi();
        var client = new AnthropicClaudeClient(
            new Monitor<AnthropicOptions>(new AnthropicOptions { ApiKey = "test-key-not-real", BaseUrl = api.BaseUrl }),
            new Monitor<AiOptions>(new AiOptions()));

        var e = await Assert.ThrowsAsync<AiUnavailableException>(() => client.CompleteAsync("σέντερ", CancellationToken.None));

        Assert.Equal(1, api.Requests);
        Assert.Equal(AiFallbackReason.HttpStatus, e.Reason);
        Assert.Equal(500, e.StatusCode);
    }

    // ---------- 4. schema + defaults ----------

    [Fact]
    public void Every_schema_property_has_a_description_and_the_two_ambiguous_ones_are_explained()
    {
        using var doc = JsonDocument.Parse(ClaudeParser.SchemaJson);
        var props = doc.RootElement.GetProperty("properties");

        foreach (var p in props.EnumerateObject())
            Assert.False(string.IsNullOrWhiteSpace(p.Value.TryGetProperty("description", out var d) ? d.GetString() : null), $"{p.Name} has no description");

        var q = props.GetProperty("q").GetProperty("description").GetString()!;
        Assert.Contains("ONLY", q);
        Assert.Contains("name", q);
        Assert.Contains("never", q, StringComparison.OrdinalIgnoreCase);
        var looking = props.GetProperty("looking").GetProperty("description").GetString()!;
        Assert.Contains("looking for a team", looking);
    }

    [Fact]
    public void Nullable_fields_use_anyOf_and_never_a_type_array_or_a_null_enum_member()
    {
        using var doc = JsonDocument.Parse(ClaudeParser.SchemaJson);
        var props = doc.RootElement.GetProperty("properties");
        var nullable = new[] { "gender", "minHeight", "maxHeight", "minAge", "maxAge", "region", "league", "q" };

        foreach (var p in props.EnumerateObject())
        {
            Assert.False(p.Value.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.Array, $"{p.Name}: type array");
            Assert.False(p.Value.TryGetProperty("enum", out var topEnum) && topEnum.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.Null), $"{p.Name}: null inside enum");
            if (!nullable.Contains(p.Name)) continue;
            var any = p.Value.GetProperty("anyOf").EnumerateArray().ToList();
            Assert.Equal(2, any.Count);
            Assert.Contains(any, a => a.GetProperty("type").GetString() == "null");
            Assert.DoesNotContain(any, a => a.TryGetProperty("enum", out var e) && e.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.Null));
        }
        var gender = props.GetProperty("gender").GetProperty("anyOf").EnumerateArray().First(a => a.GetProperty("type").GetString() == "string");
        Assert.Contains("Female", gender.GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("Attica", props.GetProperty("region").GetRawText());
    }

    [Fact]
    public void Timeout_defaults_to_ten_seconds_in_code_and_in_appsettings()
    {
        Assert.Equal(10, new AiOptions().TimeoutSeconds);
        var shipped = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json")).Build();
        Assert.Equal(10, shipped.GetValue<double>("Ai:TimeoutSeconds"));
    }

    // ---------- 4. fallback reason is logged, never the query ----------

    private sealed class CaptureLogger : ILogger<GuardedNlQueryParser>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private const string Secret = "ΜΥΣΤΙΚΟΣΑΡΙΘΜΟΣ777";

    private sealed class ThrowingModel(Func<CancellationToken, Task<SearchFilters>> impl) : INlQueryParser
    {
        public Task<SearchFilters> ParseAsync(string query, CancellationToken ct = default) => impl(ct);
    }

    private sealed class ScriptedClient(string stop, string? text) : IClaudeClient
    {
        public Task<ClaudeReply> CompleteAsync(string query, CancellationToken ct) => Task.FromResult(new ClaudeReply(stop, text));
    }

    private static (GuardedNlQueryParser Guard, CaptureLogger Log) Guard(INlQueryParser model, string? key = "test-key-not-real", Action<AiOptions>? ai = null)
    {
        var options = new AiOptions();
        ai?.Invoke(options);
        var log = new CaptureLogger();
        return (new GuardedNlQueryParser(new RuleParser(), model, new Monitor<AiOptions>(options),
            new Monitor<AnthropicOptions>(new AnthropicOptions { ApiKey = key }), () => "s", log), log);
    }

    private static void AssertLoggedOnce(CaptureLogger log, string reason)
    {
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains(reason, entry.Message);
        Assert.DoesNotContain(Secret, entry.Message);
        Assert.DoesNotContain("σέντερ", entry.Message);
    }

    private static string Query => $"σέντερ Κρήτη {Secret}";

    [Fact]
    public async Task Refusal_is_logged()
    {
        var (guard, log) = Guard(new ClaudeParser(new ScriptedClient("refusal", null)));
        var f = await guard.ParseAsync(Query);
        Assert.Equal(ParsedBy.Rules, f.ParsedBy);
        AssertLoggedOnce(log, "refusal");
    }

    [Fact]
    public async Task Max_tokens_is_logged()
    {
        var (guard, log) = Guard(new ClaudeParser(new ScriptedClient("max_tokens", "{")));
        await guard.ParseAsync(Query);
        AssertLoggedOnce(log, "max_tokens");
    }

    [Fact]
    public async Task Invalid_json_is_logged()
    {
        var (guard, log) = Guard(new ClaudeParser(new ScriptedClient("end_turn", "not json")));
        await guard.ParseAsync(Query);
        AssertLoggedOnce(log, "invalid_json");
    }

    [Fact]
    public async Task Timeout_is_logged()
    {
        var (guard, log) = Guard(new ThrowingModel(async ct => { await Task.Delay(TimeSpan.FromSeconds(5), ct); return new SearchFilters(); }),
            ai: o => o.TimeoutSeconds = 0.05);
        await guard.ParseAsync(Query);
        AssertLoggedOnce(log, "timeout");
    }

    [Fact]
    public async Task Http_status_is_logged_with_the_code()
    {
        var (guard, log) = Guard(new ThrowingModel(_ => throw new AiUnavailableException("API error", AiFallbackReason.HttpStatus, 529)));
        await guard.ParseAsync(Query);
        AssertLoggedOnce(log, "http_status");
        Assert.Contains("529", log.Entries[0].Message);
    }

    [Fact]
    public async Task Disabled_and_cap_are_logged()
    {
        var (off, offLog) = Guard(new ThrowingModel(_ => throw new InvalidOperationException("must not be called")), key: null);
        await off.ParseAsync(Query);
        AssertLoggedOnce(offLog, "disabled");

        var (capped, capLog) = Guard(Model(), ai: o => o.MaxCallsPerSession = 1);
        await capped.ParseAsync("πρώτο ερώτημα"); // uses the single call; an answered model call logs nothing
        capLog.Entries.Clear();
        await capped.ParseAsync(Query);
        AssertLoggedOnce(capLog, "cap");
    }

    private static ThrowingModel Model() => new(_ => Task.FromResult(new SearchFilters { Positions = ["C"], ParsedBy = ParsedBy.Ai }));
}
