using System;
using System.Collections.Generic;

namespace AiBenchmark.Net.Models;

/// <summary>
/// Record of an invoked function or tool during an AI execution.
/// </summary>
public sealed record ToolInvocationRecord
{
    /// <summary>Name of the tool or function called.</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>Arguments supplied to the tool.</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; init; } = new Dictionary<string, object?>();

    /// <summary>Output returned by the tool execution, if captured.</summary>
    public string? Result { get; init; }

    /// <summary>Execution duration of the tool call.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Indicates whether the tool execution was successful.</summary>
    public bool Success { get; init; } = true;

    /// <summary>Error message if the tool failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>MCP server name if this tool was executed via Model Context Protocol.</summary>
    public string? McpServer { get; init; }

    /// <summary>True if this tool belongs to an MCP server.</summary>
    public bool IsMcp => !string.IsNullOrWhiteSpace(McpServer);
}
