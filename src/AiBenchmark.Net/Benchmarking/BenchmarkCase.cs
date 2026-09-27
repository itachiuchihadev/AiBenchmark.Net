using System.Collections.Generic;

namespace AiBenchmark.Net.Benchmarking;

/// <summary>
/// A declarative test case representing a prompt, expected tool, and SLA thresholds for dataset evaluation.
/// </summary>
public sealed class BenchmarkCase
{
    /// <summary>Friendly identifier or name of the benchmark case.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The prompt string to test.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Optional expected tool name that should be triggered.</summary>
    public string? ExpectedTool { get; set; }

    /// <summary>Optional expected substring in the response.</summary>
    public string? ExpectedSubstring { get; set; }

    /// <summary>Maximum cost allowed in USD.</summary>
    public decimal? MaxCostUsd { get; set; }

    /// <summary>Maximum duration allowed in milliseconds.</summary>
    public double? MaxDurationMs { get; set; }

    /// <summary>Maximum TTFT latency allowed in milliseconds.</summary>
    public double? MaxTtftMs { get; set; }

    /// <summary>Optional category or tags.</summary>
    public string? Category { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="BenchmarkCase"/>.
    /// </summary>
    public BenchmarkCase(string name, string prompt, string? expectedTool = null, decimal? maxCostUsd = null)
    {
        Name = name;
        Prompt = prompt;
        ExpectedTool = expectedTool;
        MaxCostUsd = maxCostUsd;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BenchmarkCase"/>.
    /// </summary>
    public BenchmarkCase() { }
}
