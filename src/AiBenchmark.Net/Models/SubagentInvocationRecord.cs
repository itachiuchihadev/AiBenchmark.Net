using System;

namespace AiBenchmark.Net.Models;

/// <summary>
/// Record of a subagent delegation in a hierarchical or multi-agent system.
/// </summary>
public sealed record SubagentInvocationRecord
{
    /// <summary>Role or identifier of the delegated subagent (e.g. "researcher", "coder").</summary>
    public string AgentRole { get; init; } = string.Empty;

    /// <summary>Role of the parent or delegating agent (e.g. "orchestrator").</summary>
    public string? ParentAgentRole { get; init; }

    /// <summary>Task or instruction given to the subagent.</summary>
    public string? TaskDescription { get; init; }

    /// <summary>Duration of the subagent's execution.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Tokens consumed by the subagent.</summary>
    public TokenUsageSummary Tokens { get; init; } = TokenUsageSummary.Empty;

    /// <summary>Estimated monetary cost in USD attributable to this subagent.</summary>
    public decimal CostUsd { get; init; }

    /// <summary>Indicates whether the subagent task succeeded.</summary>
    public bool Success { get; init; } = true;

    /// <summary>Error message if the subagent failed.</summary>
    public string? ErrorMessage { get; init; }
}
