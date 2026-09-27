using System;
using AiBenchmark.Net.Models;

namespace AiBenchmark.Net.Reporting;

/// <summary>
/// Criteria used to filter AI execution reports for analytics and HTML reporting.
/// </summary>
public sealed class ReportFilterCriteria
{
    /// <summary>Filter for records occurring on or after this timestamp.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Filter for records occurring on or before this timestamp.</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>Filter by specific model identifier or substring (case-insensitive).</summary>
    public string? ModelId { get; set; }

    /// <summary>Filter by AI provider or vendor name (case-insensitive).</summary>
    public string? Provider { get; set; }

    /// <summary>Filter by customer or tenant ID.</summary>
    public string? TenantId { get; set; }

    /// <summary>Filter by user ID.</summary>
    public string? UserId { get; set; }

    /// <summary>Filter by operation or task label.</summary>
    public string? Operation { get; set; }

    /// <summary>Filter by execution outcome (true = success only, false = failure only, null = all).</summary>
    public bool? Success { get; set; }

    /// <summary>Filter by streaming mode (true = streaming only, false = non-streaming only, null = all).</summary>
    public bool? IsStreaming { get; set; }

    /// <summary>Minimum duration in milliseconds (useful for identifying latency outliers).</summary>
    public double? MinDurationMs { get; set; }

    /// <summary>Minimum estimated cost in USD.</summary>
    public decimal? MinCostUsd { get; set; }

    /// <summary>Maximum number of records to include in the report.</summary>
    public int? MaxRecords { get; set; }

    /// <summary>
    /// Evaluates whether an <see cref="AiBenchmarkReport"/> satisfies all specified criteria.
    /// </summary>
    public bool Matches(AiBenchmarkReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (From.HasValue && report.Timestamp < From.Value) return false;
        if (To.HasValue && report.Timestamp > To.Value) return false;

        if (!string.IsNullOrWhiteSpace(ModelId) &&
            !report.ModelId.Contains(ModelId, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrWhiteSpace(Provider) &&
            !report.Provider.Contains(Provider, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrWhiteSpace(TenantId) &&
            !string.Equals(report.TenantId, TenantId, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrWhiteSpace(UserId) &&
            !string.Equals(report.UserId, UserId, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrWhiteSpace(Operation) &&
            !string.Equals(report.Operation, Operation, StringComparison.OrdinalIgnoreCase)) return false;

        if (Success.HasValue && report.Success != Success.Value) return false;
        if (IsStreaming.HasValue && report.IsStreaming != IsStreaming.Value) return false;
        if (MinDurationMs.HasValue && report.DurationMs < MinDurationMs.Value) return false;
        if (MinCostUsd.HasValue && report.EstimatedCostUsd < MinCostUsd.Value) return false;

        return true;
    }
}
