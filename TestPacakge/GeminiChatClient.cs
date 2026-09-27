using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace TestPacakge;

/// <summary>
/// A real <see cref="IChatClient"/> implementation communicating directly with the Google Gemini API.
/// Supports both non-streaming and Server-Sent Events (SSE) streaming with full usage metadata extraction.
/// </summary>
public sealed class GeminiChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _defaultModel;
    private readonly ChatClientMetadata _metadata;

    /// <inheritdoc />
    public ChatClientMetadata Metadata => _metadata;

    /// <summary>
    /// Initializes a new instance of <see cref="GeminiChatClient"/>.
    /// </summary>
    /// <param name="apiKey">Gemini API key. Defaults to Environment.GetEnvironmentVariable("GEMINI_API_KEY").</param>
    /// <param name="modelId">Target model ID. Defaults to "gemini-3.5-flash-lite".</param>
    /// <param name="httpClient">Optional shared HttpClient instance.</param>
    public GeminiChatClient(string? apiKey = null, string modelId = "gemini-3.5-flash-lite", HttpClient? httpClient = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException("Gemini API key is required. Set the GEMINI_API_KEY environment variable or pass it to GeminiChatClient.");

        _defaultModel = modelId;
        _httpClient = httpClient ?? new HttpClient();

        _metadata = new ChatClientMetadata(
            providerName: "Google",
            providerUri: new Uri("https://generativelanguage.googleapis.com"),
            defaultModelId: modelId);
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatMessages);

        string model = ResolveModel(options);
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_apiKey}";

        var payload = BuildGeminiPayload(chatMessages, options);
        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage? response = null;
        string responseJson = string.Empty;

        for (int attempt = 0; attempt < 4; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if ((int)response.StatusCode == 429 && attempt < 3)
            {
                await Task.Delay(3500, cancellationToken).ConfigureAwait(false);
                continue;
            }
            break;
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Gemini API error (Status: {response?.StatusCode}): {responseJson}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        string outputText = string.Empty;
        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            if (first.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts))
            {
                var sb = new StringBuilder();
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textElem))
                    {
                        sb.Append(textElem.GetString());
                    }
                }
                outputText = sb.ToString();
            }
        }

        var usage = ExtractUsage(root);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, outputText))
        {
            ModelId = model,
            Usage = usage
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatMessages);

        string model = ResolveModel(options);
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:streamGenerateContent?alt=sse&key={_apiKey}";

        var payload = BuildGeminiPayload(chatMessages, options);
        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage? response = null;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
            response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if ((int)response.StatusCode == 429 && attempt < 3)
            {
                response.Dispose();
                await Task.Delay(3500, cancellationToken).ConfigureAwait(false);
                continue;
            }
            break;
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            string errorBody = response != null ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) : "No response";
            response?.Dispose();
            throw new HttpRequestException($"Gemini Streaming API error (Status: {response?.StatusCode}): {errorBody}");
        }

        using var responseScope = response;
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        UsageDetails? finalUsage = null;

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("data: ", StringComparison.OrdinalIgnoreCase))
            {
                string json = line.Substring(6).Trim();
                if (string.IsNullOrWhiteSpace(json)) continue;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Extract any text part
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var first = candidates[0];
                    if (first.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts))
                    {
                        foreach (var part in parts.EnumerateArray())
                        {
                            if (part.TryGetProperty("text", out var textElem))
                            {
                                string textChunk = textElem.GetString() ?? string.Empty;
                                if (!string.IsNullOrEmpty(textChunk))
                                {
                                    yield return new ChatResponseUpdate
                                    {
                                        Contents = new List<AIContent> { new TextContent(textChunk) },
                                        Role = ChatRole.Assistant
                                    };
                                }
                            }
                        }
                    }
                }

                // Check for usageMetadata update
                var currentUsage = ExtractUsage(root);
                if (currentUsage != null)
                {
                    finalUsage = currentUsage;
                }
            }
        }

        // Emit final chunk with UsageContent
        if (finalUsage != null)
        {
            yield return new ChatResponseUpdate
            {
                Contents = new List<AIContent> { new UsageContent(finalUsage) }
            };
        }
    }

    private string ResolveModel(ChatOptions? options)
    {
        return !string.IsNullOrWhiteSpace(options?.ModelId) ? options.ModelId : _defaultModel;
    }

    private static object BuildGeminiPayload(IEnumerable<ChatMessage> chatMessages, ChatOptions? options)
    {
        var contentsList = new List<object>();

        foreach (var msg in chatMessages)
        {
            string role = msg.Role == ChatRole.Assistant ? "model" : "user";
            contentsList.Add(new
            {
                role = role,
                parts = new object[] { new { text = msg.Text ?? string.Empty } }
            });
        }

        var payload = new Dictionary<string, object>
        {
            ["contents"] = contentsList
        };

        if (options?.Temperature.HasValue == true || options?.MaxOutputTokens.HasValue == true)
        {
            var genConfig = new Dictionary<string, object>();
            if (options.Temperature.HasValue) genConfig["temperature"] = options.Temperature.Value;
            if (options.MaxOutputTokens.HasValue) genConfig["maxOutputTokens"] = options.MaxOutputTokens.Value;
            payload["generationConfig"] = genConfig;
        }

        return payload;
    }

    private static UsageDetails? ExtractUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var usageElem))
        {
            return null;
        }

        long inputTokens = 0;
        long outputTokens = 0;
        long totalTokens = 0;
        long cachedTokens = 0;

        if (usageElem.TryGetProperty("promptTokenCount", out var pt)) inputTokens = pt.GetInt64();
        if (usageElem.TryGetProperty("candidatesTokenCount", out var ct)) outputTokens = ct.GetInt64();
        if (usageElem.TryGetProperty("totalTokenCount", out var tt)) totalTokens = tt.GetInt64();
        if (usageElem.TryGetProperty("cachedContentTokenCount", out var cached)) cachedTokens = cached.GetInt64();

        var additional = new AdditionalPropertiesDictionary<long>();
        if (cachedTokens > 0)
        {
            additional["cached_tokens"] = cachedTokens;
        }

        return new UsageDetails
        {
            InputTokenCount = inputTokens,
            OutputTokenCount = outputTokens,
            TotalTokenCount = totalTokens > 0 ? totalTokens : (inputTokens + outputTokens),
            AdditionalCounts = additional.Count > 0 ? additional : null
        };
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(ChatClientMetadata))
        {
            return _metadata;
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Don't dispose shared HttpClient if not owned
    }
}
