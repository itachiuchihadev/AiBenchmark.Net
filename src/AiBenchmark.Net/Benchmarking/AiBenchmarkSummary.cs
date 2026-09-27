using System;
using System.Collections.Generic;
using System.Linq;

namespace AiBenchmark.Net.Benchmarking;

/// <summary>
/// Contains the aggregated summary results from an <see cref="AiBenchmarkRunner"/> execution.
/// </summary>
public sealed class AiBenchmarkSummary
{
    /// <summary>Suite name or benchmark class name.</summary>
    public string SuiteName { get; init; } = "AI Benchmarks";

    /// <summary>Target benchmark class type.</summary>
    public Type? BenchmarkType { get; init; }

    /// <summary>All benchmark results executed during the run.</summary>
    public IReadOnlyList<AiBenchmarkResult> Results { get; init; } = Array.Empty<AiBenchmarkResult>();

    /// <summary>Total benchmark test cases executed.</summary>
    public int TotalScenarios => Results.Count;

    /// <summary>Total assertions evaluated across all scenarios.</summary>
    public int TotalAssertions => Results.Sum(r => r.Assertions.Count);

    /// <summary>Total assertions that passed.</summary>
    public int PassedAssertions => Results.Sum(r => r.Assertions.Count(a => a.Passed));

    /// <summary>Total assertions that failed.</summary>
    public int FailedAssertions => Results.Sum(r => r.Assertions.Count(a => !a.Passed));

    /// <summary>Whether all assertions passed across the entire suite.</summary>
    public bool AllAssertionsPassed => FailedAssertions == 0;

    /// <summary>Total tokens consumed across all benchmark scenarios.</summary>
    public long TotalTokens => Results.Sum(r => r.TotalTokens);

    /// <summary>Total estimated USD cost across all benchmark scenarios.</summary>
    public decimal TotalCostUsd => Results.Sum(r => r.EstimatedCostUsd);

    /// <summary>File path to the auto-generated HTML report, if generated.</summary>
    public string? HtmlReportPath { get; set; }

    /// <summary>Total duration of the benchmark suite run.</summary>
    public TimeSpan TotalExecutionDuration { get; init; }
}
