using System;

namespace AiBenchmark.Net.Models;

/// <summary>
/// Represents the outcome of an assertion evaluated against an AI invocation.
/// </summary>
public sealed record AssertionResult
{
    /// <summary>Name or rule description of the assertion (e.g., "Cost &lt;= $0.0002").</summary>
    public string AssertionName { get; init; } = string.Empty;

    /// <summary>Category or target being asserted (e.g. "Cost", "Latency", "Tool", "Response", "MCP", "Subagent").</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Indicates whether the assertion passed.</summary>
    public bool Passed { get; init; }

    /// <summary>Actual value observed during the invocation.</summary>
    public string ActualValue { get; init; } = string.Empty;

    /// <summary>Expected threshold or condition.</summary>
    public string ExpectedValue { get; init; } = string.Empty;

    /// <summary>Detailed diagnostic message if the assertion failed.</summary>
    public string? FailureMessage { get; init; }

    /// <summary>
    /// Creates a successful assertion result.
    /// </summary>
    public static AssertionResult Pass(string category, string name, string actual) => new()
    {
        Category = category,
        AssertionName = name,
        Passed = true,
        ActualValue = actual,
        ExpectedValue = name
    };

    /// <summary>
    /// Creates a failed assertion result.
    /// </summary>
    public static AssertionResult Fail(string category, string name, string expected, string actual, string? message = null) => new()
    {
        Category = category,
        AssertionName = name,
        Passed = false,
        ExpectedValue = expected,
        ActualValue = actual,
        FailureMessage = message ?? $"Assertion failed. Expected: {expected}, Actual: {actual}"
    };
}
