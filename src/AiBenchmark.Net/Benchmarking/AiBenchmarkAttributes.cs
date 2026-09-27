using System;
using System.Text.RegularExpressions;

namespace AiBenchmark.Net.Benchmarking;

/// <summary>
/// Marks a class as an AI Benchmark Suite, similar to BenchmarkDotNet.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AiBenchmarkSuiteAttribute : Attribute
{
    public string Name { get; }
    public string? Description { get; set; }

    public AiBenchmarkSuiteAttribute(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Marks a method as an AI benchmark test scenario.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class AiBenchmarkAttribute : Attribute
{
    public string? Description { get; set; }
    public bool IsStreaming { get; set; }
    public bool Offline { get; set; }
    public string? ModelId { get; set; }

    public AiBenchmarkAttribute(string? description = null)
    {
        Description = description;
    }
}

/// <summary>
/// Provides arguments for a parameterized benchmark scenario, similar to BenchmarkDotNet's [Arguments].
/// Can be applied multiple times to execute the benchmark method across multiple scenarios.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class AiArgumentsAttribute : Attribute
{
    public object?[] Arguments { get; }

    public AiArgumentsAttribute(params object?[] arguments)
    {
        Arguments = arguments ?? Array.Empty<object?>();
    }
}

/// <summary>
/// Defines parameter matrix values on a property or field, similar to BenchmarkDotNet's [Params].
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class AiParamsAttribute : Attribute
{
    public object[] Values { get; }

    public AiParamsAttribute(params object[] values)
    {
        Values = values ?? Array.Empty<object>();
    }
}

/// <summary>
/// Base class for all declarative benchmark assertions.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public abstract class AiAssertionAttribute : Attribute
{
    /// <summary>
    /// Validates the benchmark result against this assertion rule.
    /// </summary>
    public abstract void Validate(AiBenchmarkResult result);
}

/// <summary>
/// Asserts that total estimated USD cost is at or below the specified limit.
/// </summary>
public sealed class AiAssertCostAttribute : AiAssertionAttribute
{
    public decimal MaxCostUsd { get; }

    public AiAssertCostAttribute(double maxCostUsd)
    {
        MaxCostUsd = (decimal)maxCostUsd;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertCost(MaxCostUsd);
}

/// <summary>
/// Asserts that end-to-end request duration does not exceed the specified threshold in milliseconds.
/// </summary>
public sealed class AiAssertDurationAttribute : AiAssertionAttribute
{
    public double MaxDurationMs { get; }

    public AiAssertDurationAttribute(double maxDurationMs)
    {
        MaxDurationMs = maxDurationMs;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertDuration(MaxDurationMs);
}

/// <summary>
/// Asserts that streaming Time-To-First-Token does not exceed the specified threshold in milliseconds.
/// </summary>
public sealed class AiAssertTimeToFirstTokenAttribute : AiAssertionAttribute
{
    public double MaxTtftMs { get; }

    public AiAssertTimeToFirstTokenAttribute(double maxTtftMs)
    {
        MaxTtftMs = maxTtftMs;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertTimeToFirstToken(MaxTtftMs);
}

/// <summary>
/// Asserts that input and/or output token consumption does not exceed specified ceilings.
/// </summary>
public sealed class AiAssertTokensAttribute : AiAssertionAttribute
{
    public long? MaxInput { get; }
    public long? MaxOutput { get; }

    public AiAssertTokensAttribute(long maxInput = -1, long maxOutput = -1)
    {
        MaxInput = maxInput >= 0 ? maxInput : null;
        MaxOutput = maxOutput >= 0 ? maxOutput : null;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertTokens(MaxInput, MaxOutput);
}

/// <summary>
/// Asserts that the response text contains the expected substring.
/// </summary>
public sealed class AiAssertResponseContainsAttribute : AiAssertionAttribute
{
    public string ExpectedSubstring { get; }
    public StringComparison Comparison { get; }

    public AiAssertResponseContainsAttribute(string expectedSubstring, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        ExpectedSubstring = expectedSubstring;
        Comparison = comparison;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertResponseContains(ExpectedSubstring, Comparison);
}

/// <summary>
/// Asserts that the response text matches a regular expression.
/// </summary>
public sealed class AiAssertResponseMatchesAttribute : AiAssertionAttribute
{
    public string Pattern { get; }
    public RegexOptions Options { get; }

    public AiAssertResponseMatchesAttribute(string pattern, RegexOptions options = RegexOptions.IgnoreCase)
    {
        Pattern = pattern;
        Options = options;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertResponseMatches(Pattern, Options);
}

/// <summary>
/// Asserts that the response text is valid JSON.
/// </summary>
public sealed class AiAssertValidJsonAttribute : AiAssertionAttribute
{
    public override void Validate(AiBenchmarkResult result) => result.AssertValidJson();
}

/// <summary>
/// Asserts that the response text length falls within min and max character bounds.
/// </summary>
public sealed class AiAssertResponseLengthAttribute : AiAssertionAttribute
{
    public int? MinChars { get; }
    public int? MaxChars { get; }

    public AiAssertResponseLengthAttribute(int minChars = -1, int maxChars = -1)
    {
        MinChars = minChars >= 0 ? minChars : null;
        MaxChars = maxChars >= 0 ? maxChars : null;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertResponseLength(MinChars, MaxChars);
}

/// <summary>
/// Asserts that a specific tool or function was called during execution.
/// </summary>
public sealed class AiAssertToolCalledAttribute : AiAssertionAttribute
{
    public string ToolName { get; }

    public AiAssertToolCalledAttribute(string toolName)
    {
        ToolName = toolName;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertToolCalled(ToolName);
}

/// <summary>
/// Asserts the total number of tool invocations.
/// </summary>
public sealed class AiAssertToolCallCountAttribute : AiAssertionAttribute
{
    public int ExpectedCount { get; }

    public AiAssertToolCallCountAttribute(int expectedCount)
    {
        ExpectedCount = expectedCount;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertToolCallCount(ExpectedCount);
}

/// <summary>
/// Asserts that an MCP server tool was called.
/// </summary>
public sealed class AiAssertMcpToolCalledAttribute : AiAssertionAttribute
{
    public string ServerName { get; }
    public string ToolName { get; }

    public AiAssertMcpToolCalledAttribute(string serverName, string toolName)
    {
        ServerName = serverName;
        ToolName = toolName;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertMcpToolCalled(ServerName, ToolName);
}

/// <summary>
/// Asserts that an MCP resource URI was accessed.
/// </summary>
public sealed class AiAssertMcpResourceAccessedAttribute : AiAssertionAttribute
{
    public string ResourceUri { get; }

    public AiAssertMcpResourceAccessedAttribute(string resourceUri)
    {
        ResourceUri = resourceUri;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertMcpResourceAccessed(ResourceUri);
}

/// <summary>
/// Asserts the maximum number of calls made to an MCP server.
/// </summary>
public sealed class AiAssertMcpServerCountAttribute : AiAssertionAttribute
{
    public string ServerName { get; }
    public int MaxCalls { get; }

    public AiAssertMcpServerCountAttribute(string serverName, int maxCalls)
    {
        ServerName = serverName;
        MaxCalls = maxCalls;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertMcpServerCount(ServerName, MaxCalls);
}

/// <summary>
/// Asserts that a subagent was delegated to by role.
/// </summary>
public sealed class AiAssertSubagentInvokedAttribute : AiAssertionAttribute
{
    public string AgentRole { get; }

    public AiAssertSubagentInvokedAttribute(string agentRole)
    {
        AgentRole = agentRole;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertSubagentInvoked(AgentRole);
}

/// <summary>
/// Asserts total subagents invoked.
/// </summary>
public sealed class AiAssertSubagentCountAttribute : AiAssertionAttribute
{
    public int ExpectedCount { get; }

    public AiAssertSubagentCountAttribute(int expectedCount)
    {
        ExpectedCount = expectedCount;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertSubagentCount(ExpectedCount);
}

/// <summary>
/// Asserts that a subagent's total spend is within the specified limit.
/// </summary>
public sealed class AiAssertSubagentSpendAttribute : AiAssertionAttribute
{
    public string AgentRole { get; }
    public decimal MaxCostUsd { get; }

    public AiAssertSubagentSpendAttribute(string agentRole, double maxCostUsd)
    {
        AgentRole = agentRole;
        MaxCostUsd = (decimal)maxCostUsd;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertSubagentSpend(AgentRole, MaxCostUsd);
}

/// <summary>
/// Asserts that subagents were invoked in an exact sequence.
/// </summary>
public sealed class AiAssertDelegationSequenceAttribute : AiAssertionAttribute
{
    public string[] ExpectedSequence { get; }

    public AiAssertDelegationSequenceAttribute(params string[] expectedSequence)
    {
        ExpectedSequence = expectedSequence;
    }

    public override void Validate(AiBenchmarkResult result) => result.AssertDelegationSequence(ExpectedSequence);
}
