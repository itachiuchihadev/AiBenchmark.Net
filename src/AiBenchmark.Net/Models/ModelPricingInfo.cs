using System;

namespace AiBenchmark.Net.Models;

/// <summary>
/// Defines token pricing rates for a given AI model in USD per 1,000,000 tokens.
/// </summary>
public sealed record ModelPricingInfo
{
    /// <summary>
    /// Gets the identifier or prefix of the model (e.g., "gpt-4o", "claude-3-5-sonnet").
    /// </summary>
    public string ModelId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional provider or vendor name (e.g., "OpenAI", "Anthropic", "Azure").
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// Gets the price in USD per 1,000,000 standard input tokens.
    /// </summary>
    public decimal InputPricePerMillion { get; init; }

    /// <summary>
    /// Gets the price in USD per 1,000,000 output tokens.
    /// </summary>
    public decimal OutputPricePerMillion { get; init; }

    /// <summary>
    /// Gets the price in USD per 1,000,000 cached input tokens (prompt cache).
    /// Defaults to half or discounted input price if specified.
    /// </summary>
    public decimal CachedInputPricePerMillion { get; init; }

    /// <summary>
    /// Free pricing tier (e.g. for local models or Ollama).
    /// </summary>
    public static ModelPricingInfo Free(string modelId, string? provider = null) => new()
    {
        ModelId = modelId,
        Provider = provider,
        InputPricePerMillion = 0m,
        OutputPricePerMillion = 0m,
        CachedInputPricePerMillion = 0m
    };

    /// <summary>
    /// Calculates the estimated cost in USD for the specified token counts.
    /// </summary>
    public decimal CalculateCost(long inputTokens, long outputTokens, long cachedTokens = 0)
    {
        long nonCachedInput = Math.Max(0, inputTokens - cachedTokens);
        long cachedInput = Math.Min(inputTokens, Math.Max(0, cachedTokens));

        decimal nonCachedCost = (nonCachedInput / 1_000_000m) * InputPricePerMillion;
        decimal cachedCost = (cachedInput / 1_000_000m) * CachedInputPricePerMillion;
        decimal outputCost = (Math.Max(0, outputTokens) / 1_000_000m) * OutputPricePerMillion;

        return Math.Round(nonCachedCost + cachedCost + outputCost, 6);
    }
}
