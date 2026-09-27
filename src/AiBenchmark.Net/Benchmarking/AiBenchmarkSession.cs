using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiBenchmark.Net.Abstractions;
using AiBenchmark.Net.Models;
using AiBenchmark.Net.Pricing;
using AiBenchmark.Net.Reporting;
using Microsoft.Extensions.AI;

namespace AiBenchmark.Net.Benchmarking;

/// <summary>
/// Dedicated benchmarking session and assertion runner for LLM calls.
/// Autonomously executes benchmark prompts, records multi-dimensional metrics,
/// evaluates assertions, and automatically compiles light-mode HTML reports on disposal.
/// </summary>
public sealed class AiBenchmarkSession : IAsyncDisposable, IDisposable
{
    private readonly List<AiBenchmarkResult> _results = new();
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private bool _disposed;

    /// <summary>Name of the benchmark suite.</summary>
    public string SuiteName { get; set; } = "LLM Benchmark Suite";

    /// <summary>Directory where HTML reports are automatically written.</summary>
    public string ReportDirectory { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "AiBenchmarkReports");

    /// <summary>Whether to automatically generate an HTML report when the session completes.</summary>
    public bool AutoGenerateHtmlReport { get; set; } = true;

    /// <summary>Whether to automatically launch the generated HTML report in the default browser.</summary>
    public bool AutoOpenReport { get; set; } = false;

    /// <summary>The pricing engine used to estimate costs.</summary>
    public IModelPricingEngine PricingEngine { get; set; } = new DefaultModelPricingEngine();

    /// <summary>All benchmark results executed within this session.</summary>
    public IReadOnlyList<AiBenchmarkResult> Results
    {
        get
        {
            lock (_lock) return _results.ToList();
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AiBenchmarkSession"/>.
    /// </summary>
    public AiBenchmarkSession(Action<AiBenchmarkSession>? configure = null)
    {
        configure?.Invoke(this);
    }

    /// <summary>
    /// Benchmarks a single prompt against an <see cref="IChatClient"/> with automated metrics capture.
    /// </summary>
    public async Task<AiBenchmarkResult> BenchmarkAsync(
        string name,
        IChatClient client,
        string prompt,
        BenchmarkExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        return await BenchmarkAsync(
            name,
            client,
            new[] { new ChatMessage(ChatRole.User, prompt) },
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Benchmarks chat messages against an <see cref="IChatClient"/> with automated metrics capture.
    /// </summary>
    public async Task<AiBenchmarkResult> BenchmarkAsync(
        string name,
        IChatClient client,
        IEnumerable<ChatMessage> messages,
        BenchmarkExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(messages);

        options ??= new BenchmarkExecutionOptions();
        var messageList = messages.ToList();

        string modelId = !string.IsNullOrWhiteSpace(options.ModelId)
            ? options.ModelId
            : client.GetService<ChatClientMetadata>()?.DefaultModelId ?? "unknown";

        string provider = client.GetService<ChatClientMetadata>()?.ProviderName ?? "Unknown";

        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        TimeSpan? timeToFirstToken = null;
        string responseText = string.Empty;
        UsageDetails? usage = null;
        bool isStreaming = options.IsStreaming;
        bool success = true;
        string? errorMessage = null;

        var chatOptions = options.ChatOptions ?? new ChatOptions();
        if (!string.IsNullOrWhiteSpace(options.ModelId)) chatOptions.ModelId = options.ModelId;

        try
        {
            if (isStreaming)
            {
                var sb = new StringBuilder();
                await foreach (var chunk in client.GetStreamingResponseAsync(messageList, chatOptions, cancellationToken).ConfigureAwait(false))
                {
                    if (timeToFirstToken == null && (!string.IsNullOrEmpty(chunk.Text) || chunk.Contents.Count > 0))
                    {
                        timeToFirstToken = stopwatch.Elapsed;
                    }

                    if (!string.IsNullOrEmpty(chunk.Text))
                    {
                        sb.Append(chunk.Text);
                    }

                    foreach (var content in chunk.Contents)
                    {
                        if (content is UsageContent uc)
                        {
                            usage = uc.Details;
                        }
                    }
                }
                responseText = sb.ToString();
            }
            else
            {
                var response = await client.GetResponseAsync(messageList, chatOptions, cancellationToken).ConfigureAwait(false);
                responseText = response.Text ?? string.Empty;
                usage = response.Usage;
            }
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            long inputTokens = usage?.InputTokenCount ?? Math.Max(1, messageList.Sum(m => (m.Text ?? "").Length) / 4);
            long outputTokens = usage?.OutputTokenCount ?? Math.Max(1, responseText.Length / 4);
            long cachedTokens = 0;
            long reasoningTokens = 0;

            if (usage?.AdditionalCounts != null)
            {
                foreach (var kvp in usage.AdditionalCounts)
                {
                    if (kvp.Key.Contains("cache", StringComparison.OrdinalIgnoreCase)) cachedTokens += kvp.Value;
                    if (kvp.Key.Contains("reasoning", StringComparison.OrdinalIgnoreCase)) reasoningTokens += kvp.Value;
                }
            }

            decimal cost = PricingEngine.CalculateCost(modelId, inputTokens, outputTokens, cachedTokens, provider);

            string promptText = string.Join("\n", messageList.Select(m => m.Text));

            var report = new AiBenchmarkReport
            {
                ModelId = modelId,
                Provider = provider,
                Duration = stopwatch.Elapsed,
                TimeToFirstToken = timeToFirstToken,
                Prompt = promptText,
                ResponseText = responseText,
                TokenUsage = new TokenUsageSummary
                {
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    CachedInputTokens = cachedTokens,
                    ReasoningTokens = reasoningTokens
                },
                EstimatedCostUsd = cost,
                Timestamp = startTime,
                IsStreaming = isStreaming,
                Success = success,
                ErrorMessage = errorMessage,
                TenantId = options.TenantId,
                UserId = options.UserId,
                Operation = options.Operation,
                ToolInvocations = options.ToolInvocations.ToList(),
                McpInvocations = options.McpInvocations.ToList(),
                SubagentInvocations = options.SubagentInvocations.ToList()
            };

            var result = new AiBenchmarkResult(name, report);
            lock (_lock)
            {
                _results.Add(result);
            }
        }

        lock (_lock)
        {
            return _results[^1];
        }
    }

    /// <summary>
    /// Evaluates a prompt offline using local tokenizer estimation without making costly external LLM network calls.
    /// Ideal for validating prompt budgets, token bounds, and template sizing in CI/CD.
    /// </summary>
    public AiBenchmarkResult EvaluatePromptOffline(
        string prompt,
        string modelId = "gemini-3.5-flash-lite",
        string? provider = "Google",
        string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        string testName = name ?? $"Offline Sizing [{modelId}]";
        long estimatedInputTokens = Math.Max(1, prompt.Length / 4);
        decimal estimatedCost = PricingEngine.CalculateCost(modelId, estimatedInputTokens, 0, 0, provider);

        var report = new AiBenchmarkReport
        {
            ModelId = modelId,
            Provider = provider ?? "Local",
            Duration = TimeSpan.FromMilliseconds(0.5),
            Prompt = prompt,
            ResponseText = "[Offline Evaluation: No LLM network call invoked]",
            TokenUsage = new TokenUsageSummary
            {
                InputTokens = estimatedInputTokens,
                OutputTokens = 0,
                CachedInputTokens = 0
            },
            EstimatedCostUsd = estimatedCost,
            Timestamp = DateTimeOffset.UtcNow,
            IsStreaming = false,
            Success = true
        };

        var result = new AiBenchmarkResult(testName, report);
        lock (_lock)
        {
            _results.Add(result);
        }
        return result;
    }

    /// <summary>
    /// Evaluates a dataset of benchmark prompt cases, applying assertions across each test case.
    /// </summary>
    public async Task<IReadOnlyList<AiBenchmarkResult>> EvaluateDatasetAsync(
        IEnumerable<BenchmarkCase> dataset,
        IChatClient client,
        Action<BenchmarkCase, AiBenchmarkResult>? assertions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(client);

        var list = new List<AiBenchmarkResult>();

        foreach (var testCase in dataset)
        {
            var options = new BenchmarkExecutionOptions
            {
                Operation = testCase.Category
            };

            var result = await BenchmarkAsync(testCase.Name, client, testCase.Prompt, options, cancellationToken).ConfigureAwait(false);

            if (testCase.MaxCostUsd.HasValue) result.AssertCost(testCase.MaxCostUsd.Value);
            if (testCase.MaxDurationMs.HasValue) result.AssertDuration(testCase.MaxDurationMs.Value);
            if (testCase.MaxTtftMs.HasValue) result.AssertTimeToFirstToken(testCase.MaxTtftMs.Value);
            if (!string.IsNullOrEmpty(testCase.ExpectedSubstring)) result.AssertResponseContains(testCase.ExpectedSubstring);
            if (!string.IsNullOrEmpty(testCase.ExpectedTool)) result.AssertToolCalled(testCase.ExpectedTool);

            assertions?.Invoke(testCase, result);
            list.Add(result);
        }

        return list;
    }

    /// <summary>
    /// File path to the last generated HTML report, if generated.
    /// </summary>
    public string? LastGeneratedReportPath { get; private set; }

    /// <summary>
    /// Manually records a custom or intercepted report into the benchmark session.
    /// </summary>
    public AiBenchmarkResult Record(string name, AiBenchmarkReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var result = new AiBenchmarkResult(name, report);
        lock (_lock)
        {
            _results.Add(result);
        }
        return result;
    }

    /// <summary>
    /// Compiles and generates the light-mode HTML dashboard immediately, returning its file path.
    /// </summary>
    public string? GenerateHtmlReport()
    {
        return GenerateAndPublishReport();
    }

    /// <summary>
    /// Automatically generates the HTML report when the session is disposed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await Task.Run(GenerateAndPublishReport).ConfigureAwait(false);
    }

    /// <summary>
    /// Automatically generates the HTML report when the session is disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        GenerateAndPublishReport();
    }

    private string? GenerateAndPublishReport()
    {
        if (!AutoGenerateHtmlReport) return null;

        List<AiBenchmarkReport> reports;
        lock (_lock)
        {
            if (_results.Count == 0) return null;
            reports = _results.Select(r => r.Report).ToList();
        }

        try
        {
            Directory.CreateDirectory(ReportDirectory);
            string safeSuiteName = string.Join("_", SuiteName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
            string fileName = $"Benchmark_{safeSuiteName}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.html";
            string fullPath = Path.Combine(ReportDirectory, fileName);

            AiBenchmarkHtmlReport.SaveToFile(fullPath, reports, SuiteName);
            LastGeneratedReportPath = fullPath;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[AiBenchmark.Net] Auto-generated Benchmark Report: {fullPath}");
            Console.ResetColor();

            if (AutoOpenReport)
            {
                AiBenchmarkHtmlReport.OpenInBrowser(fullPath);
            }

            return fullPath;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[AiBenchmark.Net] Failed to auto-generate report: {ex.Message}");
            Console.ResetColor();
            return null;
        }
    }
}
