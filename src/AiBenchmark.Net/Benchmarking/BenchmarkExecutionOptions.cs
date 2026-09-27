using System.Collections.Generic;
using AiBenchmark.Net.Models;
using Microsoft.Extensions.AI;

namespace AiBenchmark.Net.Benchmarking;

/// <summary>
/// Configuration options for executing an AI benchmark invocation.
/// </summary>
public sealed class BenchmarkExecutionOptions
{
    /// <summary>Target model ID override.</summary>
    public string? ModelId { get; set; }

    /// <summary>Whether to benchmark using streaming mode.</summary>
    public bool IsStreaming { get; set; }

    /// <summary>Optional tenant ID tag.</summary>
    public string? TenantId { get; set; }

    /// <summary>Optional user ID tag.</summary>
    public string? UserId { get; set; }

    /// <summary>Optional operation or test category tag.</summary>
    public string? Operation { get; set; }

    /// <summary>Optional simulated or intercepted tool invocations.</summary>
    public List<ToolInvocationRecord> ToolInvocations { get; } = new();

    /// <summary>Optional simulated or intercepted MCP invocations.</summary>
    public List<McpInvocationRecord> McpInvocations { get; } = new();

    /// <summary>Optional simulated or intercepted subagent invocations.</summary>
    public List<SubagentInvocationRecord> SubagentInvocations { get; } = new();

    /// <summary>Underlying ChatOptions to supply to the IChatClient.</summary>
    public ChatOptions? ChatOptions { get; set; }
}
