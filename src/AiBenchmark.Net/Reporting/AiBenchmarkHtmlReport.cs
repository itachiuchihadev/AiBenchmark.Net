using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using AiBenchmark.Net.Abstractions;
using AiBenchmark.Net.Models;

namespace AiBenchmark.Net.Reporting;

/// <summary>
/// Generates clean, professional light-mode visual HTML reports for AiBenchmark.Net benchmarking.
/// Supports filtering by time, model, provider, tenant, latency, and status criteria.
/// </summary>
public static class AiBenchmarkHtmlReport
{
    /// <summary>
    /// Generates an HTML report from a collection of <see cref="AiBenchmarkReport"/> records matching the given criteria.
    /// </summary>
    public static string GenerateHtml(
        IEnumerable<AiBenchmarkReport> reports,
        string? title = null,
        ReportFilterCriteria? criteria = null)
    {
        ArgumentNullException.ThrowIfNull(reports);

        criteria ??= new ReportFilterCriteria();
        var filteredList = reports.Where(criteria.Matches).ToList();

        if (criteria.MaxRecords.HasValue && filteredList.Count > criteria.MaxRecords.Value)
        {
            filteredList = filteredList.Take(criteria.MaxRecords.Value).ToList();
        }

        string reportTitle = title ?? "AiBenchmark.Net — AI Benchmarking & Guardrails";
        var generatedAt = DateTimeOffset.UtcNow;

        long totalRequests = filteredList.Count;
        long successfulRequests = filteredList.Count(r => r.Success);
        long failedRequests = totalRequests - successfulRequests;
        long totalInputTokens = filteredList.Sum(r => r.InputTokens);
        long totalOutputTokens = filteredList.Sum(r => r.OutputTokens);
        long totalCachedTokens = filteredList.Sum(r => r.CachedInputTokens);
        long totalTokens = totalInputTokens + totalOutputTokens;
        decimal totalCost = filteredList.Sum(r => r.EstimatedCostUsd);
        double avgDuration = totalRequests > 0 ? filteredList.Average(r => r.DurationMs) : 0;

        var streamingReports = filteredList.Where(r => r.TimeToFirstTokenMs.HasValue).ToList();
        double? avgTtft = streamingReports.Count > 0 ? streamingReports.Average(r => r.TimeToFirstTokenMs!.Value) : null;
        double successRate = totalRequests > 0 ? (double)successfulRequests / totalRequests * 100.0 : 100.0;

        // Group by model for chart / summaries
        var modelGroups = filteredList
            .GroupBy(r => string.IsNullOrWhiteSpace(r.ModelId) ? "unknown" : r.ModelId)
            .Select(g => new
            {
                Model = g.Key,
                Count = g.Count(),
                Cost = g.Sum(r => r.EstimatedCostUsd),
                Tokens = g.Sum(r => r.TotalTokens),
                InputTokens = g.Sum(r => r.InputTokens),
                OutputTokens = g.Sum(r => r.OutputTokens)
            })
            .OrderByDescending(g => g.Cost)
            .ToList();

        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"  <title>{WebUtility.HtmlEncode(reportTitle)}</title>");
        sb.AppendLine("  <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.AppendLine("  <link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");
        sb.AppendLine("  <link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap\" rel=\"stylesheet\">");
        sb.AppendLine("  <style>");
        sb.AppendLine(@"
    :root {
      --bg: #f8fafc;
      --surface: #ffffff;
      --surface-subtle: #f1f5f9;
      --border: #e2e8f0;
      --border-focus: #94a3b8;
      --text-heading: #0f172a;
      --text-body: #334155;
      --text-muted: #64748b;
      --primary: #2563eb;
      --primary-hover: #1d4ed8;
      --primary-soft: #eff6ff;
      --success: #059669;
      --success-bg: #ecfdf5;
      --success-border: #a7f3d0;
      --error: #dc2626;
      --error-bg: #fef2f2;
      --error-border: #fecaca;
      --warning: #d97706;
      --warning-bg: #fffbeb;
      --warning-border: #fde68a;
      --shadow-sm: 0 1px 2px 0 rgba(0, 0, 0, 0.05);
      --shadow-card: 0 1px 3px 0 rgba(0, 0, 0, 0.08), 0 1px 2px -1px rgba(0, 0, 0, 0.06);
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      background-color: var(--bg);
      color: var(--text-body);
      font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      font-size: 0.875rem;
      line-height: 1.5;
      -webkit-font-smoothing: antialiased;
      -moz-osx-font-smoothing: grayscale;
      padding: 2.5rem 1.5rem;
      min-height: 100vh;
    }
    .container { max-width: 1360px; margin: 0 auto; }
    
    /* Header */
    .header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      flex-wrap: wrap;
      gap: 1.25rem;
      margin-bottom: 2rem;
      padding-bottom: 1.5rem;
      border-bottom: 1px solid var(--border);
    }
    .brand-row { display: flex; align-items: center; gap: 0.75rem; }
    .brand-badge {
      background: #0f172a;
      color: #ffffff;
      padding: 0.35rem 0.65rem;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 700;
      letter-spacing: 0.04em;
    }
    .header-title h1 {
      font-size: 1.5rem;
      font-weight: 700;
      color: var(--text-heading);
      letter-spacing: -0.02em;
    }
    .header-title p {
      color: var(--text-muted);
      font-size: 0.8125rem;
      margin-top: 0.25rem;
    }
    .header-stats {
      display: flex;
      gap: 0.75rem;
      align-items: center;
    }
    .stat-pill {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 6px;
      padding: 0.35rem 0.75rem;
      font-size: 0.8125rem;
      font-weight: 600;
      color: var(--text-heading);
      box-shadow: var(--shadow-sm);
    }
    
    /* Criteria Bar */
    .criteria-card {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 0.75rem 1rem;
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 1.75rem;
      box-shadow: var(--shadow-sm);
    }
    .criteria-label {
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin-right: 0.25rem;
    }
    .filter-chip {
      display: inline-flex;
      align-items: center;
      padding: 0.2rem 0.65rem;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 500;
      background: var(--surface-subtle);
      border: 1px solid var(--border);
      color: var(--text-body);
    }
    .filter-chip.active {
      background: var(--primary-soft);
      border-color: #bfdbfe;
      color: var(--primary);
      font-weight: 600;
    }

    /* KPI Grid */
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 1.25rem;
      margin-bottom: 2rem;
    }
    .kpi-card {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 10px;
      padding: 1.25rem 1.5rem;
      box-shadow: var(--shadow-card);
      display: flex;
      flex-direction: column;
    }
    .kpi-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.5rem;
    }
    .kpi-label {
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--text-muted);
    }
    .kpi-value {
      font-size: 1.75rem;
      font-weight: 700;
      color: var(--text-heading);
      letter-spacing: -0.02em;
    }
    .kpi-sub {
      font-size: 0.75rem;
      color: var(--text-muted);
      margin-top: 0.25rem;
    }

    /* Analytics Grid */
    .analytics-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1.5rem;
      margin-bottom: 2rem;
    }
    @media (max-width: 900px) { .analytics-grid { grid-template-columns: 1fr; } }
    .card {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 10px;
      padding: 1.5rem;
      box-shadow: var(--shadow-card);
    }
    .card h3 {
      font-size: 0.9375rem;
      font-weight: 600;
      color: var(--text-heading);
      margin-bottom: 1.25rem;
    }

    .bar-row { margin-bottom: 1rem; }
    .bar-row:last-child { margin-bottom: 0; }
    .bar-label {
      display: flex;
      justify-content: space-between;
      font-size: 0.8125rem;
      margin-bottom: 0.35rem;
    }
    .bar-track {
      height: 7px;
      background: #e2e8f0;
      border-radius: 4px;
      overflow: hidden;
    }
    .bar-fill {
      height: 100%;
      border-radius: 4px;
      transition: width 0.4s ease;
    }

    /* Table Toolbar */
    .table-toolbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 1rem;
      margin-bottom: 1rem;
    }
    .search-input {
      background: var(--surface);
      border: 1px solid var(--border);
      color: var(--text-heading);
      padding: 0.55rem 0.9rem;
      border-radius: 6px;
      font-size: 0.8125rem;
      font-family: inherit;
      width: 320px;
      outline: none;
      box-shadow: var(--shadow-sm);
      transition: border-color 0.15s;
    }
    .search-input:focus { border-color: var(--primary); }
    .btn-group { display: flex; gap: 0.4rem; flex-wrap: wrap; }
    .btn-filter {
      background: var(--surface);
      border: 1px solid var(--border);
      color: var(--text-body);
      padding: 0.45rem 0.8rem;
      border-radius: 6px;
      font-size: 0.8125rem;
      font-weight: 500;
      cursor: pointer;
      box-shadow: var(--shadow-sm);
      transition: all 0.15s ease;
    }
    .btn-filter:hover { background: var(--surface-subtle); color: var(--text-heading); }
    .btn-filter.active {
      background: var(--primary);
      border-color: var(--primary);
      color: #ffffff;
    }

    /* Table */
    .table-card {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 10px;
      box-shadow: var(--shadow-card);
      overflow: hidden;
    }
    .table-responsive { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; text-align: left; }
    th {
      background: #f8fafc;
      color: var(--text-muted);
      font-weight: 600;
      font-size: 0.6875rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      padding: 0.75rem 1rem;
      border-bottom: 1px solid var(--border);
      white-space: nowrap;
    }
    td {
      padding: 0.75rem 1rem;
      border-bottom: 1px solid #f1f5f9;
      color: var(--text-body);
      font-size: 0.8125rem;
    }
    tr:last-child td { border-bottom: none; }
    tr:hover td { background: #f8fafc; }

    /* Badges & Chips */
    .code-chip {
      font-family: 'JetBrains Mono', Consolas, monospace;
      font-size: 0.75rem;
      background: var(--surface-subtle);
      border: 1px solid var(--border);
      padding: 0.15rem 0.45rem;
      border-radius: 4px;
      color: var(--text-heading);
      font-weight: 500;
    }
    .pill {
      display: inline-block;
      padding: 0.15rem 0.5rem;
      border-radius: 4px;
      font-size: 0.6875rem;
      font-weight: 600;
      letter-spacing: 0.02em;
    }
    .pill-success { background: var(--success-bg); color: var(--success); border: 1px solid var(--success-border); }
    .pill-error { background: var(--error-bg); color: var(--error); border: 1px solid var(--error-border); }
    .pill-stream { background: var(--primary-soft); color: var(--primary); border: 1px solid #bfdbfe; }
    .pill-sync { background: var(--surface-subtle); color: var(--text-muted); border: 1px solid var(--border); }
    .cost-text { font-weight: 600; color: #047857; }
  ");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        // Header
        sb.AppendLine("    <div class=\"header\">");
        sb.AppendLine("      <div class=\"header-title\">");
        sb.AppendLine("        <div class=\"brand-row\">");
        sb.AppendLine("          <span class=\"brand-badge\">AiBenchmark.Net</span>");
        sb.AppendLine($"          <h1>{WebUtility.HtmlEncode(reportTitle)}</h1>");
        sb.AppendLine("        </div>");
        sb.AppendLine($"        <p>Benchmark telemetry, token consumption & spend analytics • Generated {generatedAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"header-stats\">");
        sb.AppendLine($"        <span class=\"stat-pill\">{totalRequests} Total Calls</span>");
        sb.AppendLine($"        <span class=\"stat-pill\">${totalCost:F6} Total Spend</span>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Criteria bar
        sb.AppendLine("    <div class=\"criteria-card\">");
        sb.AppendLine("      <span class=\"criteria-label\">Filters Applied:</span>");
        sb.AppendLine($"      <span class=\"filter-chip\">Model: {(string.IsNullOrWhiteSpace(criteria.ModelId) ? "All" : WebUtility.HtmlEncode(criteria.ModelId))}</span>");
        sb.AppendLine($"      <span class=\"filter-chip\">Provider: {(string.IsNullOrWhiteSpace(criteria.Provider) ? "All" : WebUtility.HtmlEncode(criteria.Provider))}</span>");
        if (!string.IsNullOrWhiteSpace(criteria.TenantId))
            sb.AppendLine($"      <span class=\"filter-chip active\">Tenant: {WebUtility.HtmlEncode(criteria.TenantId)}</span>");
        if (!string.IsNullOrWhiteSpace(criteria.UserId))
            sb.AppendLine($"      <span class=\"filter-chip active\">User: {WebUtility.HtmlEncode(criteria.UserId)}</span>");
        if (!string.IsNullOrWhiteSpace(criteria.Operation))
            sb.AppendLine($"      <span class=\"filter-chip active\">Operation: {WebUtility.HtmlEncode(criteria.Operation)}</span>");
        if (criteria.Success.HasValue)
            sb.AppendLine($"      <span class=\"filter-chip active\">Status: {(criteria.Success.Value ? "Success" : "Failed")}</span>");
        if (criteria.IsStreaming.HasValue)
            sb.AppendLine($"      <span class=\"filter-chip active\">Mode: {(criteria.IsStreaming.Value ? "Streaming" : "Sync")}</span>");
        if (criteria.MinDurationMs.HasValue)
            sb.AppendLine($"      <span class=\"filter-chip active\">Min Latency: {criteria.MinDurationMs.Value:F0}ms</span>");
        sb.AppendLine("    </div>");

        // Assertions stats
        long totalAssertions = filteredList.Sum(r => r.Assertions.Count);
        long passedAssertions = filteredList.Sum(r => r.Assertions.Count(a => a.Passed));
        long failedAssertions = totalAssertions - passedAssertions;
        double assertionPassRate = totalAssertions > 0 ? (double)passedAssertions / totalAssertions * 100.0 : 100.0;

        // KPI Grid
        sb.AppendLine("    <div class=\"kpi-grid\">");

        // 1. Total Spend
        sb.AppendLine("      <div class=\"kpi-card\">");
        sb.AppendLine("        <div class=\"kpi-header\"><span class=\"kpi-label\">Total Spend</span></div>");
        sb.AppendLine($"        <div class=\"kpi-value\" style=\"color: #047857;\">${totalCost:F6}</div>");
        sb.AppendLine($"        <div class=\"kpi-sub\">Across {totalRequests} invocations</div>");
        sb.AppendLine("      </div>");

        // 2. Total Tokens
        sb.AppendLine("      <div class=\"kpi-card\">");
        sb.AppendLine("        <div class=\"kpi-header\"><span class=\"kpi-label\">Total Tokens</span></div>");
        sb.AppendLine($"        <div class=\"kpi-value\">{totalTokens:N0}</div>");
        sb.AppendLine($"        <div class=\"kpi-sub\">Input: {totalInputTokens:N0} • Output: {totalOutputTokens:N0}</div>");
        sb.AppendLine("      </div>");

        // 3. Prompt Cache
        sb.AppendLine("      <div class=\"kpi-card\">");
        sb.AppendLine("        <div class=\"kpi-header\"><span class=\"kpi-label\">Prompt Cache Hits</span></div>");
        sb.AppendLine($"        <div class=\"kpi-value\">{totalCachedTokens:N0}</div>");
        double cachePct = totalInputTokens > 0 ? (double)totalCachedTokens / totalInputTokens * 100.0 : 0;
        sb.AppendLine($"        <div class=\"kpi-sub\">{cachePct:F1}% of input tokens served from cache</div>");
        sb.AppendLine("      </div>");

        // 4. Latency
        sb.AppendLine("      <div class=\"kpi-card\">");
        sb.AppendLine("        <div class=\"kpi-header\"><span class=\"kpi-label\">Avg Latency</span></div>");
        sb.AppendLine($"        <div class=\"kpi-value\">{avgDuration:F1} <span style=\"font-size:0.95rem; color:var(--text-muted); font-weight:500;\">ms</span></div>");
        sb.AppendLine($"        <div class=\"kpi-sub\">Streaming TTFT: {(avgTtft.HasValue ? $"{avgTtft.Value:F1} ms" : "N/A")}</div>");
        sb.AppendLine("      </div>");

        // 5. Assertions Guardrails
        sb.AppendLine("      <div class=\"kpi-card\">");
        sb.AppendLine("        <div class=\"kpi-header\"><span class=\"kpi-label\">SLA & Budget Assertions</span></div>");
        string assertionColor = failedAssertions > 0 ? "#dc2626" : "#047857";
        sb.AppendLine($"        <div class=\"kpi-value\" style=\"color: {assertionColor};\">{assertionPassRate:F0}%</div>");
        sb.AppendLine($"        <div class=\"kpi-sub\">{passedAssertions} passed • {failedAssertions} failed (Total {totalAssertions})</div>");
        sb.AppendLine("      </div>");

        sb.AppendLine("    </div>");

        // Analytics Grid (Spend by Model & Tokens by Model)
        sb.AppendLine("    <div class=\"analytics-grid\">");

        // Spend Breakdown
        sb.AppendLine("      <div class=\"card\">");
        sb.AppendLine("        <h3>Spend by Model</h3>");
        decimal maxCost = modelGroups.Count > 0 ? Math.Max(0.000001m, modelGroups.Max(m => m.Cost)) : 1m;
        foreach (var m in modelGroups)
        {
            double pct = (double)(m.Cost / maxCost * 100m);
            sb.AppendLine("        <div class=\"bar-row\">");
            sb.AppendLine("          <div class=\"bar-label\">");
            sb.AppendLine($"            <span class=\"code-chip\">{WebUtility.HtmlEncode(m.Model)}</span>");
            sb.AppendLine($"            <strong>${m.Cost:F6} ({m.Count} calls)</strong>");
            sb.AppendLine("          </div>");
            sb.AppendLine("          <div class=\"bar-track\">");
            sb.AppendLine($"            <div class=\"bar-fill\" style=\"width: {pct:F1}%; background: #059669;\"></div>");
            sb.AppendLine("          </div>");
            sb.AppendLine("        </div>");
        }
        sb.AppendLine("      </div>");

        // Token Breakdown
        sb.AppendLine("      <div class=\"card\">");
        sb.AppendLine("        <h3>Token Volume by Model</h3>");
        long maxTokens = modelGroups.Count > 0 ? Math.Max(1, modelGroups.Max(m => m.Tokens)) : 1;
        foreach (var m in modelGroups)
        {
            double pct = (double)m.Tokens / maxTokens * 100.0;
            sb.AppendLine("        <div class=\"bar-row\">");
            sb.AppendLine("          <div class=\"bar-label\">");
            sb.AppendLine($"            <span class=\"code-chip\">{WebUtility.HtmlEncode(m.Model)}</span>");
            sb.AppendLine($"            <span>{m.Tokens:N0} tokens (In: {m.InputTokens:N0} / Out: {m.OutputTokens:N0})</span>");
            sb.AppendLine("          </div>");
            sb.AppendLine("          <div class=\"bar-track\">");
            sb.AppendLine($"            <div class=\"bar-fill\" style=\"width: {pct:F1}%; background: #2563eb;\"></div>");
            sb.AppendLine("          </div>");
            sb.AppendLine("        </div>");
        }
        sb.AppendLine("      </div>");

        sb.AppendLine("    </div>");

        // Table toolbar
        sb.AppendLine("    <div class=\"table-toolbar\">");
        sb.AppendLine("      <input type=\"text\" id=\"tableSearch\" class=\"search-input\" placeholder=\"Search model, tool, subagent, prompt, or operation...\" oninput=\"filterTable()\" />");
        sb.AppendLine("      <div class=\"btn-group\">");
        sb.AppendLine("        <button class=\"btn-filter active\" onclick=\"quickFilter('all', this)\">All</button>");
        sb.AppendLine("        <button class=\"btn-filter\" onclick=\"quickFilter('stream', this)\">Streaming</button>");
        sb.AppendLine("        <button class=\"btn-filter\" onclick=\"quickFilter('sync', this)\">Non-Streaming</button>");
        sb.AppendLine("        <button class=\"btn-filter\" onclick=\"quickFilter('tools', this)\">With Tools</button>");
        sb.AppendLine("        <button class=\"btn-filter\" onclick=\"quickFilter('failed-assertions', this)\">Failed Assertions</button>");
        sb.AppendLine("        <button class=\"btn-filter\" onclick=\"quickFilter('error', this)\">Errors</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Table Card
        sb.AppendLine("    <div class=\"table-card\">");
        sb.AppendLine("      <div class=\"table-responsive\">");
        sb.AppendLine("        <table id=\"invocationsTable\">");
        sb.AppendLine("          <thead>");
        sb.AppendLine("            <tr>");
        sb.AppendLine("              <th>Time</th>");
        sb.AppendLine("              <th>Model</th>");
        sb.AppendLine("              <th>Mode</th>");
        sb.AppendLine("              <th style=\"text-align:right;\">Latency</th>");
        sb.AppendLine("              <th style=\"text-align:right;\">TTFT</th>");
        sb.AppendLine("              <th style=\"text-align:right;\">Tokens</th>");
        sb.AppendLine("              <th style=\"text-align:right;\">Cost (USD)</th>");
        sb.AppendLine("              <th style=\"text-align:center;\">Assertions</th>");
        sb.AppendLine("              <th>Tools & MCP</th>");
        sb.AppendLine("              <th>Subagents</th>");
        sb.AppendLine("              <th style=\"text-align:center;\">Status</th>");
        sb.AppendLine("            </tr>");
        sb.AppendLine("          </thead>");
        sb.AppendLine("          <tbody>");

        foreach (var r in filteredList)
        {
            string statusTag = r.Success
                ? "<span class=\"pill pill-success\">OK</span>"
                : $"<span class=\"pill pill-error\" title=\"{WebUtility.HtmlEncode(r.ErrorMessage)}\">ERROR</span>";

            string typeTag = r.IsStreaming
                ? "<span class=\"pill pill-stream\">STREAM</span>"
                : "<span class=\"pill pill-sync\">SYNC</span>";

            string ttftText = r.TimeToFirstTokenMs.HasValue ? $"{r.TimeToFirstTokenMs.Value:F1} ms" : "—";

            // Assertions badge
            string assertionsCell = "—";
            bool hasFailedAssertions = r.Assertions.Any(a => !a.Passed);
            if (r.Assertions.Count > 0)
            {
                int pCount = r.Assertions.Count(a => a.Passed);
                int fCount = r.Assertions.Count - pCount;
                if (fCount == 0)
                {
                    assertionsCell = $"<span class=\"pill pill-success\">✓ {pCount} PASSED</span>";
                }
                else
                {
                    string failuresTooltip = string.Join("; ", r.Assertions.Where(a => !a.Passed).Select(a => $"{a.Category}: {a.FailureMessage}"));
                    assertionsCell = $"<span class=\"pill pill-error\" title=\"{WebUtility.HtmlEncode(failuresTooltip)}\">❌ {fCount} FAILED</span>";
                }
            }

            // Tools / MCP Cell
            var toolBadges = new List<string>();
            foreach (var t in r.ToolInvocations)
            {
                toolBadges.Add($"<span class=\"code-chip\" title=\"Tool: {WebUtility.HtmlEncode(t.ToolName)}\">⚙ {WebUtility.HtmlEncode(t.ToolName)}</span>");
            }
            foreach (var m in r.McpInvocations)
            {
                string mcpLabel = !string.IsNullOrEmpty(m.ToolName) ? $"{m.ServerName}.{m.ToolName}" : m.ServerName;
                toolBadges.Add($"<span class=\"pill pill-stream\" title=\"MCP: {WebUtility.HtmlEncode(mcpLabel)}\">⚡ mcp:{WebUtility.HtmlEncode(mcpLabel)}</span>");
            }
            string toolsCell = toolBadges.Count > 0 ? string.Join(" ", toolBadges) : "<span style=\"color:var(--text-muted); font-size:0.75rem;\">—</span>";

            // Subagents Cell
            var agentBadges = new List<string>();
            foreach (var sub in r.SubagentInvocations)
            {
                agentBadges.Add($"<span class=\"pill pill-sync\" title=\"Subagent: {WebUtility.HtmlEncode(sub.AgentRole)}\">🤖 {WebUtility.HtmlEncode(sub.AgentRole)}</span>");
            }
            string agentsCell = agentBadges.Count > 0 ? string.Join(" ", agentBadges) : "<span style=\"color:var(--text-muted); font-size:0.75rem;\">—</span>";

            sb.AppendLine($"            <tr data-streaming=\"{r.IsStreaming.ToString().ToLowerInvariant()}\" data-success=\"{r.Success.ToString().ToLowerInvariant()}\" data-has-tools=\"{(r.ToolInvocations.Count > 0 || r.McpInvocations.Count > 0).ToString().ToLowerInvariant()}\" data-failed-assertions=\"{hasFailedAssertions.ToString().ToLowerInvariant()}\">");
            sb.AppendLine($"              <td style=\"color:var(--text-muted); font-size:0.75rem; font-family:'JetBrains Mono', monospace;\">{r.Timestamp:HH:mm:ss.fff}</td>");
            sb.AppendLine($"              <td><span class=\"code-chip\">{WebUtility.HtmlEncode(r.ModelId)}</span></td>");
            sb.AppendLine($"              <td>{typeTag}</td>");
            sb.AppendLine($"              <td style=\"text-align:right; font-family:'JetBrains Mono', monospace;\">{r.DurationMs:F1} ms</td>");
            sb.AppendLine($"              <td style=\"text-align:right; font-family:'JetBrains Mono', monospace;\">{ttftText}</td>");
            sb.AppendLine($"              <td style=\"text-align:right;\"><span style=\"font-size:0.75rem; color:var(--text-muted);\">In:</span>{r.InputTokens:N0} <span style=\"font-size:0.75rem; color:var(--text-muted);\">Out:</span>{r.OutputTokens:N0}</td>");
            sb.AppendLine($"              <td style=\"text-align:right;\" class=\"cost-text\">${r.EstimatedCostUsd:F6}</td>");
            sb.AppendLine($"              <td style=\"text-align:center;\">{assertionsCell}</td>");
            sb.AppendLine($"              <td>{toolsCell}</td>");
            sb.AppendLine($"              <td>{agentsCell}</td>");
            sb.AppendLine($"              <td style=\"text-align:center;\">{statusTag}</td>");
            sb.AppendLine("            </tr>");
        }

        sb.AppendLine("          </tbody>");
        sb.AppendLine("        </table>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Client-side quick filter & search
        sb.AppendLine(@"
    <script>
      function filterTable() {
        const query = document.getElementById('tableSearch').value.toLowerCase();
        const rows = document.querySelectorAll('#invocationsTable tbody tr');
        rows.forEach(row => {
          const text = row.innerText.toLowerCase();
          row.style.display = text.includes(query) ? '' : 'none';
        });
      }

      function quickFilter(mode, btn) {
        document.querySelectorAll('.btn-filter').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        const rows = document.querySelectorAll('#invocationsTable tbody tr');
        rows.forEach(row => {
          if (mode === 'all') row.style.display = '';
          else if (mode === 'stream') row.style.display = row.dataset.streaming === 'true' ? '' : 'none';
          else if (mode === 'sync') row.style.display = row.dataset.streaming === 'false' ? '' : 'none';
          else if (mode === 'tools') row.style.display = row.dataset.hasTools === 'true' ? '' : 'none';
          else if (mode === 'failed-assertions') row.style.display = row.dataset.failedAssertions === 'true' ? '' : 'none';
          else if (mode === 'error') row.style.display = row.dataset.success === 'false' ? '' : 'none';
        });
      }
    </script>
  </div>
</body>
</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Saves the HTML report to a file.
    /// </summary>
    public static void SaveToFile(
        string filePath,
        IEnumerable<AiBenchmarkReport> reports,
        string? title = null,
        ReportFilterCriteria? criteria = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string html = GenerateHtml(reports, title, criteria);

        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(filePath, html, Encoding.UTF8);
    }

    /// <summary>
    /// Attempts to open the generated HTML report file in the operating system's default browser.
    /// </summary>
    public static bool OpenInBrowser(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;

            var psi = new ProcessStartInfo
            {
                FileName = Path.GetFullPath(filePath),
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
