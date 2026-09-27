using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using AiBenchmark.Net.Models;

namespace AiBenchmark.Net.Pricing;

/// <summary>
/// Pre-configured catalog of popular AI model pricing rates in USD per 1,000,000 tokens.
/// Optimized with FrozenDictionary for SIMD-accelerated, zero-allocation lookups in .NET 8, 9, and 10.
/// </summary>
public static class EmbeddedPricingCatalog
{
    private static readonly FrozenDictionary<string, ModelPricingInfo> Catalog = BuildCatalog().ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, ModelPricingInfo> BuildCatalog() => new(StringComparer.OrdinalIgnoreCase)
    {
        // ─── OpenAI Models ─────────────────────────────────────────────────────────────
        ["gpt-4o"] = new()
        {
            ModelId = "gpt-4o",
            Provider = "OpenAI",
            InputPricePerMillion = 2.50m,
            OutputPricePerMillion = 10.00m,
            CachedInputPricePerMillion = 1.25m
        },
        ["gpt-4o-mini"] = new()
        {
            ModelId = "gpt-4o-mini",
            Provider = "OpenAI",
            InputPricePerMillion = 0.15m,
            OutputPricePerMillion = 0.60m,
            CachedInputPricePerMillion = 0.075m
        },
        ["gpt-4-turbo"] = new()
        {
            ModelId = "gpt-4-turbo",
            Provider = "OpenAI",
            InputPricePerMillion = 10.00m,
            OutputPricePerMillion = 30.00m,
            CachedInputPricePerMillion = 5.00m
        },
        ["gpt-4"] = new()
        {
            ModelId = "gpt-4",
            Provider = "OpenAI",
            InputPricePerMillion = 30.00m,
            OutputPricePerMillion = 60.00m,
            CachedInputPricePerMillion = 15.00m
        },
        ["gpt-3.5-turbo"] = new()
        {
            ModelId = "gpt-3.5-turbo",
            Provider = "OpenAI",
            InputPricePerMillion = 0.50m,
            OutputPricePerMillion = 1.50m,
            CachedInputPricePerMillion = 0.50m
        },
        ["o1"] = new()
        {
            ModelId = "o1",
            Provider = "OpenAI",
            InputPricePerMillion = 15.00m,
            OutputPricePerMillion = 60.00m,
            CachedInputPricePerMillion = 7.50m
        },
        ["o1-mini"] = new()
        {
            ModelId = "o1-mini",
            Provider = "OpenAI",
            InputPricePerMillion = 3.00m,
            OutputPricePerMillion = 12.00m,
            CachedInputPricePerMillion = 1.50m
        },
        ["o3-mini"] = new()
        {
            ModelId = "o3-mini",
            Provider = "OpenAI",
            InputPricePerMillion = 1.10m,
            OutputPricePerMillion = 4.40m,
            CachedInputPricePerMillion = 0.55m
        },

        // ─── Anthropic Models ──────────────────────────────────────────────────────────
        ["claude-3-5-sonnet"] = new()
        {
            ModelId = "claude-3-5-sonnet",
            Provider = "Anthropic",
            InputPricePerMillion = 3.00m,
            OutputPricePerMillion = 15.00m,
            CachedInputPricePerMillion = 0.30m
        },
        ["claude-3-5-haiku"] = new()
        {
            ModelId = "claude-3-5-haiku",
            Provider = "Anthropic",
            InputPricePerMillion = 0.80m,
            OutputPricePerMillion = 4.00m,
            CachedInputPricePerMillion = 0.08m
        },
        ["claude-3-opus"] = new()
        {
            ModelId = "claude-3-opus",
            Provider = "Anthropic",
            InputPricePerMillion = 15.00m,
            OutputPricePerMillion = 75.00m,
            CachedInputPricePerMillion = 1.50m
        },
        ["claude-3-sonnet"] = new()
        {
            ModelId = "claude-3-sonnet",
            Provider = "Anthropic",
            InputPricePerMillion = 3.00m,
            OutputPricePerMillion = 15.00m,
            CachedInputPricePerMillion = 0.75m
        },
        ["claude-3-haiku"] = new()
        {
            ModelId = "claude-3-haiku",
            Provider = "Anthropic",
            InputPricePerMillion = 0.25m,
            OutputPricePerMillion = 1.25m,
            CachedInputPricePerMillion = 0.03m
        },

        // ─── DeepSeek Models ───────────────────────────────────────────────────────────
        ["deepseek-chat"] = new()
        {
            ModelId = "deepseek-chat",
            Provider = "DeepSeek",
            InputPricePerMillion = 0.14m,
            OutputPricePerMillion = 0.28m,
            CachedInputPricePerMillion = 0.014m
        },
        ["deepseek-v3"] = new()
        {
            ModelId = "deepseek-v3",
            Provider = "DeepSeek",
            InputPricePerMillion = 0.14m,
            OutputPricePerMillion = 0.28m,
            CachedInputPricePerMillion = 0.014m
        },
        ["deepseek-reasoner"] = new()
        {
            ModelId = "deepseek-reasoner",
            Provider = "DeepSeek",
            InputPricePerMillion = 0.55m,
            OutputPricePerMillion = 2.19m,
            CachedInputPricePerMillion = 0.14m
        },
        ["deepseek-r1"] = new()
        {
            ModelId = "deepseek-r1",
            Provider = "DeepSeek",
            InputPricePerMillion = 0.55m,
            OutputPricePerMillion = 2.19m,
            CachedInputPricePerMillion = 0.14m
        },

        // ─── Google Gemini Models ──────────────────────────────────────────────────────
        ["gemini-1.5-pro"] = new()
        {
            ModelId = "gemini-1.5-pro",
            Provider = "Google",
            InputPricePerMillion = 1.25m,
            OutputPricePerMillion = 5.00m,
            CachedInputPricePerMillion = 0.3125m
        },
        ["gemini-1.5-flash"] = new()
        {
            ModelId = "gemini-1.5-flash",
            Provider = "Google",
            InputPricePerMillion = 0.075m,
            OutputPricePerMillion = 0.30m,
            CachedInputPricePerMillion = 0.01875m
        },
        ["gemini-2.0-flash"] = new()
        {
            ModelId = "gemini-2.0-flash",
            Provider = "Google",
            InputPricePerMillion = 0.10m,
            OutputPricePerMillion = 0.40m,
            CachedInputPricePerMillion = 0.025m
        },
        ["gemini-2.5-flash-lite"] = new()
        {
            ModelId = "gemini-2.5-flash-lite",
            Provider = "Google",
            InputPricePerMillion = 0.075m,
            OutputPricePerMillion = 0.30m,
            CachedInputPricePerMillion = 0.01875m
        },
        ["gemini-3.5-flash"] = new()
        {
            ModelId = "gemini-3.5-flash",
            Provider = "Google",
            InputPricePerMillion = 0.10m,
            OutputPricePerMillion = 0.40m,
            CachedInputPricePerMillion = 0.025m
        },
        ["gemini-3.5-flash-lite"] = new()
        {
            ModelId = "gemini-3.5-flash-lite",
            Provider = "Google",
            InputPricePerMillion = 0.075m,
            OutputPricePerMillion = 0.30m,
            CachedInputPricePerMillion = 0.01875m
        },
        ["gemini-flash-lite"] = new()
        {
            ModelId = "gemini-flash-lite",
            Provider = "Google",
            InputPricePerMillion = 0.075m,
            OutputPricePerMillion = 0.30m,
            CachedInputPricePerMillion = 0.01875m
        },

        // ─── Mistral AI Models ─────────────────────────────────────────────────────────
        ["mistral-large"] = new()
        {
            ModelId = "mistral-large",
            Provider = "Mistral",
            InputPricePerMillion = 2.00m,
            OutputPricePerMillion = 6.00m,
            CachedInputPricePerMillion = 1.00m
        },
        ["mistral-small"] = new()
        {
            ModelId = "mistral-small",
            Provider = "Mistral",
            InputPricePerMillion = 0.20m,
            OutputPricePerMillion = 0.60m,
            CachedInputPricePerMillion = 0.10m
        },
        ["codestral"] = new()
        {
            ModelId = "codestral",
            Provider = "Mistral",
            InputPricePerMillion = 0.30m,
            OutputPricePerMillion = 0.90m,
            CachedInputPricePerMillion = 0.15m
        }
    };

    /// <summary>
    /// Gets all catalogued model pricing information.
    /// </summary>
    public static IReadOnlyDictionary<string, ModelPricingInfo> GetAll() => Catalog;

    /// <summary>
    /// Checks if a model name corresponds to a local / self-hosted execution (e.g. Ollama).
    /// </summary>
    public static bool IsFreeOrLocalModel(string modelId, string? provider = null)
    {
        if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "Test", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (modelId.StartsWith("ollama", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("local", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("llama", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("phi", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qwen", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("gemma", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
