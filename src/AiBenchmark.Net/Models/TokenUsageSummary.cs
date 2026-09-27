using System;

namespace AiBenchmark.Net.Models;

/// <summary>
/// Represents aggregated token usage metrics for an AI invocation.
/// </summary>
public sealed record TokenUsageSummary
{
    /// <summary>
    /// Gets the number of prompt / input tokens consumed.
    /// </summary>
    public long InputTokens { get; init; }

    /// <summary>
    /// Gets the number of completion / output tokens generated.
    /// </summary>
    public long OutputTokens { get; init; }

    /// <summary>
    /// Gets the number of input tokens served from prompt cache.
    /// </summary>
    public long CachedInputTokens { get; init; }

    /// <summary>
    /// Gets the total tokens (input + output).
    /// </summary>
    public long TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// Gets the number of reasoning / thinking tokens (if applicable).
    /// </summary>
    public long ReasoningTokens { get; init; }

    /// <summary>
    /// Creates an empty token usage summary.
    /// </summary>
    public static readonly TokenUsageSummary Empty = new();

    /// <summary>
    /// Combines two token usage summaries.
    /// </summary>
    public static TokenUsageSummary operator +(TokenUsageSummary left, TokenUsageSummary right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new TokenUsageSummary
        {
            InputTokens = left.InputTokens + right.InputTokens,
            OutputTokens = left.OutputTokens + right.OutputTokens,
            CachedInputTokens = left.CachedInputTokens + right.CachedInputTokens,
            ReasoningTokens = left.ReasoningTokens + right.ReasoningTokens
        };
    }
}
