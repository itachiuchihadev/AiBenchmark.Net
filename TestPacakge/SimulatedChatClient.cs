using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace TestPacakge;

/// <summary>
/// A simulated <see cref="IChatClient"/> for demonstration and testing of the AiMetrics.Net package.
/// Generates realistic responses, token counts, and streaming latencies without requiring live API keys.
/// </summary>
public sealed class SimulatedChatClient : IChatClient
{
    private readonly ChatClientMetadata _metadata;

    public ChatClientMetadata Metadata => _metadata;

    public string ResponseText { get; set; } = "This is a simulated AI response demonstrating AiMetrics.Net observability.";
    public long InputTokens { get; set; } = 150;
    public long OutputTokens { get; set; } = 85;
    public long CachedTokens { get; set; } = 0;
    public long ReasoningTokens { get; set; } = 0;
    public int SimulatedLatencyMs { get; set; } = 40;
    public int StreamChunkCount { get; set; } = 5;
    public int StreamChunkDelayMs { get; set; } = 20;
    public bool ShouldFail { get; set; }
    public string FailureMessage { get; set; } = "Simulated rate limit exceeded (HTTP 429)";

    public SimulatedChatClient(string modelId = "gpt-4o", string providerName = "OpenAI")
    {
        _metadata = new ChatClientMetadata(
            providerName: providerName,
            providerUri: new Uri("https://api.openai.com/v1"),
            defaultModelId: modelId);
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (SimulatedLatencyMs > 0)
        {
            await Task.Delay(SimulatedLatencyMs, cancellationToken).ConfigureAwait(false);
        }

        if (ShouldFail)
        {
            throw new InvalidOperationException(FailureMessage);
        }

        var additionalCounts = new AdditionalPropertiesDictionary<long>();
        if (CachedTokens > 0)
        {
            additionalCounts["cached_tokens"] = CachedTokens;
        }
        if (ReasoningTokens > 0)
        {
            additionalCounts["reasoning_tokens"] = ReasoningTokens;
        }

        var usage = new UsageDetails
        {
            InputTokenCount = InputTokens,
            OutputTokenCount = OutputTokens,
            TotalTokenCount = InputTokens + OutputTokens,
            AdditionalCounts = additionalCounts.Count > 0 ? additionalCounts : null
        };

        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, ResponseText))
        {
            ModelId = options?.ModelId ?? Metadata.DefaultModelId,
            Usage = usage
        };

        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
        {
            await Task.Delay(SimulatedLatencyMs, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(FailureMessage);
        }

        string[] words = ResponseText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int chunkSize = Math.Max(1, words.Length / StreamChunkCount);

        for (int i = 0; i < words.Length; i += chunkSize)
        {
            if (StreamChunkDelayMs > 0)
            {
                await Task.Delay(StreamChunkDelayMs, cancellationToken).ConfigureAwait(false);
            }

            int count = Math.Min(chunkSize, words.Length - i);
            string chunk = string.Join(" ", words, i, count) + " ";

            yield return new ChatResponseUpdate
            {
                Contents = new List<AIContent> { new TextContent(chunk) },
                Role = ChatRole.Assistant
            };
        }

        // Final chunk containing UsageContent
        var additionalCounts = new AdditionalPropertiesDictionary<long>();
        if (CachedTokens > 0) additionalCounts["cached_tokens"] = CachedTokens;
        if (ReasoningTokens > 0) additionalCounts["reasoning_tokens"] = ReasoningTokens;

        var usage = new UsageDetails
        {
            InputTokenCount = InputTokens,
            OutputTokenCount = OutputTokens,
            TotalTokenCount = InputTokens + OutputTokens,
            AdditionalCounts = additionalCounts.Count > 0 ? additionalCounts : null
        };

        yield return new ChatResponseUpdate
        {
            Contents = new List<AIContent> { new UsageContent(usage) }
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(ChatClientMetadata))
        {
            return _metadata;
        }

        return null;
    }

    public void Dispose()
    {
    }
}
