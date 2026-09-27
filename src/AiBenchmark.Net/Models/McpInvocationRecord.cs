using System;
using System.Collections.Generic;

namespace AiBenchmark.Net.Models;

/// <summary>
/// Record of an interaction with a Model Context Protocol (MCP) server or resource.
/// </summary>
public sealed record McpInvocationRecord
{
    /// <summary>Name of the MCP server (e.g., "github", "filesystem", "database").</summary>
    public string ServerName { get; init; } = string.Empty;

    /// <summary>MCP tool name executed (if applicable).</summary>
    public string? ToolName { get; init; }

    /// <summary>MCP resource URI accessed (if applicable, e.g. "mcp://db/schema").</summary>
    public string? ResourceUri { get; init; }

    /// <summary>Arguments or parameters passed to the MCP server.</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; init; } = new Dictionary<string, object?>();

    /// <summary>Duration of the MCP interaction.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Indicates whether the MCP call succeeded.</summary>
    public bool Success { get; init; } = true;

    /// <summary>Error message if the call failed.</summary>
    public string? ErrorMessage { get; init; }
}
