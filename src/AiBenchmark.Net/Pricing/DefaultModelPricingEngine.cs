using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using AiBenchmark.Net.Abstractions;
using AiBenchmark.Net.Models;

namespace AiBenchmark.Net.Pricing;

/// <summary>
/// Default implementation of <see cref="IModelPricingEngine"/> supporting catalog lookups,
/// prefix/fuzzy matching, and runtime custom overrides.
/// </summary>
public sealed class DefaultModelPricingEngine : IModelPricingEngine
{
    private readonly ConcurrentDictionary<string, ModelPricingInfo> _customPricings = new(StringComparer.OrdinalIgnoreCase);
    private readonly ModelPricingInfo _defaultFallbackPricing;

    /// <summary>
    /// Initializes a new instance of <see cref="DefaultModelPricingEngine"/>.
    /// </summary>
    /// <param name="fallbackPricing">Optional fallback pricing to use when a model is unrecognized. Defaults to $0.00.</param>
    public DefaultModelPricingEngine(ModelPricingInfo? fallbackPricing = null)
    {
        _defaultFallbackPricing = fallbackPricing ?? ModelPricingInfo.Free("default");
    }

    /// <inheritdoc />
    public void RegisterPricing(ModelPricingInfo pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        ArgumentException.ThrowIfNullOrWhiteSpace(pricing.ModelId);

        _customPricings[pricing.ModelId] = pricing;
    }

    /// <summary>
    /// Helper method to easily override pricing with explicit rates.
    /// </summary>
    public void OverrideModelPricing(
        string modelId,
        decimal inputPricePerMillion,
        decimal outputPricePerMillion,
        decimal? cachedInputPricePerMillion = null,
        string? provider = null)
    {
        RegisterPricing(new ModelPricingInfo
        {
            ModelId = modelId,
            Provider = provider,
            InputPricePerMillion = inputPricePerMillion,
            OutputPricePerMillion = outputPricePerMillion,
            CachedInputPricePerMillion = cachedInputPricePerMillion ?? (inputPricePerMillion / 2m)
        });
    }

    /// <inheritdoc />
    public bool TryGetPricing(string modelId, [NotNullWhen(true)] out ModelPricingInfo? pricing, string? provider = null)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            pricing = null;
            return false;
        }

        // 1. Check custom overrides (Exact match)
        if (_customPricings.TryGetValue(modelId, out pricing))
        {
            return true;
        }

        // 2. Check embedded catalog (Exact match)
        var catalog = EmbeddedPricingCatalog.GetAll();
        if (catalog.TryGetValue(modelId, out pricing))
        {
            return true;
        }

        // 3. Prefix matching on custom overrides (e.g., "gpt-4o-2024-08-06" -> "gpt-4o")
        foreach (var entry in _customPricings)
        {
            if (modelId.StartsWith(entry.Key, StringComparison.OrdinalIgnoreCase))
            {
                pricing = entry.Value;
                return true;
            }
        }

        // 4. Prefix matching on embedded catalog
        foreach (var entry in catalog)
        {
            if (modelId.StartsWith(entry.Key, StringComparison.OrdinalIgnoreCase))
            {
                pricing = entry.Value;
                return true;
            }
        }

        // 5. Local / Ollama detection
        if (EmbeddedPricingCatalog.IsFreeOrLocalModel(modelId, provider))
        {
            pricing = ModelPricingInfo.Free(modelId, provider ?? "Local");
            return true;
        }

        pricing = null;
        return false;
    }

    /// <inheritdoc />
    public ModelPricingInfo GetPricing(string modelId, string? provider = null)
    {
        if (TryGetPricing(modelId, out var pricing, provider))
        {
            return pricing;
        }

        return _defaultFallbackPricing with { ModelId = modelId, Provider = provider };
    }

    /// <inheritdoc />
    public decimal CalculateCost(string modelId, long inputTokens, long outputTokens, long cachedTokens = 0, string? provider = null)
    {
        var pricing = GetPricing(modelId, provider);
        return pricing.CalculateCost(inputTokens, outputTokens, cachedTokens);
    }
}
