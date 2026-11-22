using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;

namespace Ballpass.Web.Features.Find.Ai;

/// <summary>
/// The only class that talks to the Anthropic API (official SDK). Key from user-secrets / env Anthropic__ApiKey only.
/// Not sent: thinking settings, temperature, assistant prefill. Every SDK failure becomes <see cref="AiUnavailableException"/>.
/// </summary>
public sealed class AnthropicClaudeClient(IOptionsMonitor<AnthropicOptions> key, IOptionsMonitor<AiOptions> ai) : IClaudeClient
{
    private static readonly Dictionary<string, JsonElement> Schema =
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ClaudeParser.SchemaJson)!;

    private AnthropicClient? client;
    private string? clientKey;

    public async Task<ClaudeReply> CompleteAsync(string query, CancellationToken ct)
    {
        var apiKey = key.CurrentValue.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey)) throw new AiUnavailableException("no API key configured");
        if (client is null || clientKey != apiKey)
        {
            var baseUrl = key.CurrentValue.BaseUrl;
            client = string.IsNullOrWhiteSpace(baseUrl)
                ? new AnthropicClient { ApiKey = apiKey, MaxRetries = 0 }
                : new AnthropicClient { ApiKey = apiKey, BaseUrl = baseUrl, MaxRetries = 0 };   // no SDK retries: one 500 = one request (a retry storm costs money and time)
            clientKey = apiKey;
        }

        try
        {
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = ai.CurrentValue.Model,
                MaxTokens = 1024,
                System = ClaudeParser.SystemPrompt,
                Messages = [new UserMessageParam { Content = query }],
                OutputConfig = new OutputConfig { Effort = Effort.Low, Format = new JsonOutputFormat { Schema = Schema } },
            }, ct);

            var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(b => b.Text));
            return new ClaudeReply(response.StopReason?.ToString(), text);
        }
        catch (AnthropicRateLimitException e)
        {
            throw new AiUnavailableException("rate or spend limit reached", AiFallbackReason.HttpStatus, 429, e);
        }
        catch (AnthropicApiException e)
        {
            throw new AiUnavailableException("API error", AiFallbackReason.HttpStatus, (int)e.StatusCode, e);
        }
    }
}
