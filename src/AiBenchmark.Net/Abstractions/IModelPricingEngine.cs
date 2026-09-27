using System.Diagnostics.CodeAnalysis;
using AiBenchmark.Net.Models;

namespace AiBenchmark.Net.Abstractions;

/// <summary>
/// Service responsible for looking up model pricing and calculating invocation costs.
/// </summary>
public interface IModelPricingEngine
{
    /// <summary>
    /// Gets pricing for the specified model and optional provider.
    /// Returns fallback/free pricing if no specific match is found.
    /// </summary>
    ModelPricingInfo GetPricing(string modelId, string? provider = null);

    /// <summary>
    /// Attempts to find pricing for the specified model and optional provider.
    /// </summary>
    bool TryGetPricing(string modelId, [NotNullWhen(true)] out ModelPricingInfo? pricing, string? provider = null);

    /// <summary>
    /// Registers or overrides pricing for a model.
    /// </summary>
    void RegisterPricing(ModelPricingInfo pricing);

    /// <summary>
    /// Calculates the estimated cost in USD for the specified token usage.
    /// </summary>
    decimal CalculateCost(string modelId, long inputTokens, long outputTokens, long cachedTokens = 0, string? provider = null);
}
