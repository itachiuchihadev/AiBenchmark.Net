using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiBenchmark.Net.Assertions;
using AiBenchmark.Net.Models;
using Microsoft.Extensions.AI;

namespace AiBenchmark.Net.Benchmarking;

/// <summary>
/// BenchmarkDotNet-style declarative test runner for AI applications and LLM pipelines.
/// Discovers benchmark classes, parameterized scenarios, and assertion guardrails,
/// executing them with real-time metric tracking and autonomous light-mode HTML reporting.
/// </summary>
public static class AiBenchmarkRunner
{
    /// <summary>
    /// Runs all benchmarks in the specified benchmark class.
    /// </summary>
    public static async Task<AiBenchmarkSummary> Run<TBenchmarkClass>(
        IChatClient? defaultClient = null,
        Action<AiBenchmarkSession>? configureSession = null,
        CancellationToken cancellationToken = default)
        where TBenchmarkClass : class
    {
        return await Run(typeof(TBenchmarkClass), defaultClient, configureSession, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs all benchmarks in the specified benchmark type.
    /// </summary>
    public static async Task<AiBenchmarkSummary> Run(
        Type benchmarkType,
        IChatClient? defaultClient = null,
        Action<AiBenchmarkSession>? configureSession = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(benchmarkType);

        var suiteAttr = benchmarkType.GetCustomAttribute<AiBenchmarkSuiteAttribute>();
        string suiteName = suiteAttr?.Name ?? benchmarkType.Name;

        var session = new AiBenchmarkSession(s =>
        {
            s.SuiteName = suiteName;
            s.AutoGenerateHtmlReport = true;
            s.AutoOpenReport = true;
            configureSession?.Invoke(s);
        });
        var stopwatch = Stopwatch.StartNew();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"// * AiBenchmark.Net Benchmark Runner *");
        Console.WriteLine($"// Benchmark Suite: {suiteName}");
        Console.WriteLine($"// Target Type:     {benchmarkType.FullName}");
        Console.WriteLine($"// Execution Date:  {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.ResetColor();
        Console.WriteLine();

        var classAssertions = benchmarkType.GetCustomAttributes<AiAssertionAttribute>(inherit: true).ToList();

        // 1. Discover [AiParams] on properties and fields
        var paramPermutations = DiscoverParamPermutations(benchmarkType);

        // 2. Discover [AiBenchmark] methods
        var benchmarkMethods = benchmarkType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<AiBenchmarkAttribute>() != null)
            .ToList();

        if (benchmarkMethods.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[AiBenchmark.Net] Warning: No methods decorated with [AiBenchmark] found in {benchmarkType.Name}.");
            Console.ResetColor();
        }

        var results = new List<AiBenchmarkResult>();

        foreach (var paramDict in paramPermutations)
        {
            // Create instance of benchmark class
            object? instance = null;
            if (!benchmarkType.IsAbstract && !benchmarkType.IsInterface)
            {
                instance = Activator.CreateInstance(benchmarkType);
                // Assign [AiParams]
                foreach (var kvp in paramDict)
                {
                    var prop = benchmarkType.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(instance, Convert.ChangeType(kvp.Value, prop.PropertyType));
                        continue;
                    }
                    var field = benchmarkType.GetField(kvp.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(instance, Convert.ChangeType(kvp.Value, field.FieldType));
                    }
                }
            }

            foreach (var method in benchmarkMethods)
            {
                var benchAttr = method.GetCustomAttribute<AiBenchmarkAttribute>()!;
                var argAttrs = method.GetCustomAttributes<AiArgumentsAttribute>().ToList();
                var methodAssertions = method.GetCustomAttributes<AiAssertionAttribute>().ToList();

                var argSets = argAttrs.Count > 0
                    ? argAttrs.Select(a => a.Arguments).ToList()
                    : new List<object?[]> { Array.Empty<object?>() };

                foreach (var args in argSets)
                {
                    string scenarioName = FormatScenarioName(method, benchAttr, paramDict, args);

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"▶ Running: {scenarioName} ... ");
                    Console.ResetColor();

                    AiBenchmarkResult? benchmarkResult = null;
                    var executionOptions = new BenchmarkExecutionOptions
                    {
                        IsStreaming = benchAttr.IsStreaming,
                        ModelId = benchAttr.ModelId
                    };

                    // Intercepting chat client to capture calls if method invokes client directly
                    var interceptor = defaultClient != null
                        ? new BenchmarkInterceptingChatClient(defaultClient, session, scenarioName, executionOptions)
                        : null;

                    try
                    {
                        object?[] methodParams = BindMethodParameters(method, args, interceptor ?? (object?)defaultClient, session, executionOptions, cancellationToken);

                        object? invokeResult = method.Invoke(instance, methodParams);

                        if (invokeResult is Task task)
                        {
                            await task.ConfigureAwait(false);
                            var taskType = task.GetType();
                            if (taskType.IsGenericType)
                            {
                                var resultProp = taskType.GetProperty("Result");
                                invokeResult = resultProp?.GetValue(task);
                            }
                            else
                            {
                                invokeResult = null;
                            }
                        }

                        // Determine result
                        if (invokeResult is AiBenchmarkResult directResult)
                        {
                            benchmarkResult = directResult;
                        }
                        else if (interceptor != null && interceptor.CapturedResult != null)
                        {
                            benchmarkResult = interceptor.CapturedResult;
                        }
                        else if (invokeResult is string promptString && !string.IsNullOrWhiteSpace(promptString))
                        {
                            if (benchAttr.Offline)
                            {
                                string targetModel = benchAttr.ModelId ?? "gemini-3.5-flash-lite";
                                benchmarkResult = session.EvaluatePromptOffline(promptString, modelId: targetModel, name: scenarioName);
                            }
                            else if (defaultClient != null)
                            {
                                benchmarkResult = await session.BenchmarkAsync(scenarioName, defaultClient, promptString, executionOptions, cancellationToken).ConfigureAwait(false);
                            }
                        }

                        if (benchmarkResult == null)
                        {
                            // If neither was captured, create an empty placeholder result
                            var fallbackReport = new AiBenchmarkReport
                            {
                                ModelId = benchAttr.ModelId ?? "unknown",
                                Provider = "Unknown",
                                Duration = TimeSpan.Zero,
                                Timestamp = DateTimeOffset.UtcNow,
                                Success = true,
                                Prompt = "[Runner Invoked Method]"
                            };
                            benchmarkResult = session.Record(scenarioName, fallbackReport);
                        }

                        // Apply declarative assertions
                        var allAssertions = classAssertions.Concat(methodAssertions);
                        foreach (var assertion in allAssertions)
                        {
                            try
                            {
                                if (assertion is AiAssertResponseContainsAttribute containAttr &&
                                    containAttr.ExpectedSubstring.Contains('{') && args.Length > 0)
                                {
                                    string formatted = string.Format(containAttr.ExpectedSubstring, args);
                                    benchmarkResult.AssertResponseContains(formatted, containAttr.Comparison);
                                }
                                else if (assertion is AiAssertResponseMatchesAttribute regexAttr &&
                                         regexAttr.Pattern.Contains('{') && args.Length > 0)
                                {
                                    string formatted = string.Format(regexAttr.Pattern, args);
                                    benchmarkResult.AssertResponseMatches(formatted, regexAttr.Options);
                                }
                                else
                                {
                                    assertion.Validate(benchmarkResult);
                                }
                            }
                            catch (AiAssertionException)
                            {
                                // Result records failure inside RecordAndValidate
                            }
                            catch (FormatException)
                            {
                                assertion.Validate(benchmarkResult);
                            }
                        }

                        results.Add(benchmarkResult);

                        if (benchmarkResult.AllAssertionsPassed)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[PASS] ({benchmarkResult.Assertions.Count}/{benchmarkResult.Assertions.Count} Assertions)");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            int passed = benchmarkResult.Assertions.Count(a => a.Passed);
                            Console.WriteLine($"[FAIL] ({passed}/{benchmarkResult.Assertions.Count} Assertions Passed)");
                        }
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[ERROR] {ex.InnerException?.Message ?? ex.Message}");
                        Console.ResetColor();

                        var errorReport = new AiBenchmarkReport
                        {
                            ModelId = benchAttr.ModelId ?? "error",
                            Provider = "Error",
                            Duration = TimeSpan.Zero,
                            Timestamp = DateTimeOffset.UtcNow,
                            Success = false,
                            ErrorMessage = ex.ToString(),
                            Prompt = scenarioName
                        };
                        var errorResult = session.Record(scenarioName, errorReport);
                        results.Add(errorResult);
                    }
                }
            }
        }

        stopwatch.Stop();

        // Print Summary Table
        PrintConsoleSummaryTable(suiteName, results);

        // Compile and publish HTML report autonomously
        string? htmlPath = session.GenerateHtmlReport();

        return new AiBenchmarkSummary
        {
            SuiteName = suiteName,
            BenchmarkType = benchmarkType,
            Results = results,
            HtmlReportPath = htmlPath,
            TotalExecutionDuration = stopwatch.Elapsed
        };
    }

    /// <summary>
    /// Discovers all benchmark classes in an assembly and runs them sequentially.
    /// </summary>
    public static async Task<IReadOnlyList<AiBenchmarkSummary>> Run(
        Assembly assembly,
        IChatClient? defaultClient = null,
        Action<AiBenchmarkSession>? configureSession = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var benchmarkTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<AiBenchmarkSuiteAttribute>() != null ||
                        t.GetMethods().Any(m => m.GetCustomAttribute<AiBenchmarkAttribute>() != null))
            .ToList();

        var summaries = new List<AiBenchmarkSummary>();
        foreach (var type in benchmarkTypes)
        {
            var summary = await Run(type, defaultClient, configureSession, cancellationToken).ConfigureAwait(false);
            summaries.Add(summary);
        }

        return summaries;
    }

    private static string FormatScenarioName(
        MethodInfo method,
        AiBenchmarkAttribute attr,
        IReadOnlyDictionary<string, object> paramDict,
        object?[] args)
    {
        var sb = new StringBuilder();
        sb.Append(attr.Description ?? method.Name);

        var details = new List<string>();
        foreach (var kvp in paramDict)
        {
            details.Add($"{kvp.Key}={kvp.Value}");
        }

        if (args.Length > 0)
        {
            var methodParams = method.GetParameters();
            for (int i = 0; i < args.Length && i < methodParams.Length; i++)
            {
                string valStr = args[i]?.ToString() ?? "null";
                if (valStr.Length > 25) valStr = valStr.Substring(0, 22) + "...";
                details.Add($"{methodParams[i].Name}=\"{valStr}\"");
            }
        }

        if (details.Count > 0)
        {
            sb.Append(" (").Append(string.Join(", ", details)).Append(")");
        }

        return sb.ToString();
    }

    private static object?[] BindMethodParameters(
        MethodInfo method,
        object?[] args,
        object? client,
        AiBenchmarkSession session,
        BenchmarkExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var parameters = method.GetParameters();
        var bound = new object?[parameters.Length];
        int argIndex = 0;

        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (typeof(IChatClient).IsAssignableFrom(p.ParameterType))
            {
                bound[i] = client;
            }
            else if (typeof(AiBenchmarkSession).IsAssignableFrom(p.ParameterType))
            {
                bound[i] = session;
            }
            else if (typeof(BenchmarkExecutionOptions).IsAssignableFrom(p.ParameterType))
            {
                bound[i] = options;
            }
            else if (p.ParameterType == typeof(CancellationToken))
            {
                bound[i] = cancellationToken;
            }
            else if (argIndex < args.Length)
            {
                var val = args[argIndex++];
                bound[i] = val != null ? Convert.ChangeType(val, Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType) : null;
            }
            else if (p.HasDefaultValue)
            {
                bound[i] = p.DefaultValue;
            }
            else
            {
                bound[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
            }
        }

        return bound;
    }

    private static List<Dictionary<string, object>> DiscoverParamPermutations(Type type)
    {
        var paramMap = new Dictionary<string, object[]>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<AiParamsAttribute>();
            if (attr != null && attr.Values.Length > 0)
            {
                paramMap[prop.Name] = attr.Values;
            }
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var attr = field.GetCustomAttribute<AiParamsAttribute>();
            if (attr != null && attr.Values.Length > 0)
            {
                paramMap[field.Name] = attr.Values;
            }
        }

        if (paramMap.Count == 0)
        {
            return new List<Dictionary<string, object>> { new() };
        }

        var results = new List<Dictionary<string, object>> { new() };

        foreach (var entry in paramMap)
        {
            var next = new List<Dictionary<string, object>>();
            foreach (var current in results)
            {
                foreach (var val in entry.Value)
                {
                    var clone = new Dictionary<string, object>(current)
                    {
                        [entry.Key] = val
                    };
                    next.Add(clone);
                }
            }
            results = next;
        }

        return results;
    }

    private static void PrintConsoleSummaryTable(string suiteName, IReadOnlyList<AiBenchmarkResult> results)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"// * Summary: {suiteName} *");
        Console.ResetColor();

        string header = $"| {"Method / Scenario",-40} | {"In Tokens",9} | {"Out Tokens",10} | {"TTFT",9} | {"Mean Latency",12} | {"Cost ($)",10} | {"Assertions",12} |";
        string separator = $"|{new string('-', 42)}|{new string('-', 11)}|{new string('-', 12)}|{new string('-', 11)}|{new string('-', 14)}|{new string('-', 12)}|{new string('-', 14)}|";

        Console.WriteLine(separator);
        Console.WriteLine(header);
        Console.WriteLine(separator);

        foreach (var r in results)
        {
            string name = r.Name.Length > 40 ? r.Name.Substring(0, 37) + "..." : r.Name;
            string inTokens = r.InputTokens.ToString("N0");
            string outTokens = r.OutputTokens.ToString("N0");
            string ttft = r.TimeToFirstTokenMs.HasValue ? $"{r.TimeToFirstTokenMs.Value:F0} ms" : "-";
            string latency = $"{r.DurationMs:F0} ms";
            string cost = $"${r.EstimatedCostUsd:F6}";
            int passedCount = r.Assertions.Count(a => a.Passed);
            string assertions = r.Assertions.Count > 0 ? $"{passedCount}/{r.Assertions.Count} PASS" : "0 Evaluated";

            if (!r.AllAssertionsPassed || !r.Success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.WriteLine($"| {name,-40} | {inTokens,9} | {outTokens,10} | {ttft,9} | {latency,12} | {cost,10} | {assertions,12} |");
        }

        Console.ResetColor();
        Console.WriteLine(separator);
        Console.WriteLine();
    }

    /// <summary>
    /// An internal IChatClient wrapper that records calls made inside benchmark methods.
    /// </summary>
    private sealed class BenchmarkInterceptingChatClient : DelegatingChatClient
    {
        private readonly AiBenchmarkSession _session;
        private readonly string _scenarioName;
        private readonly BenchmarkExecutionOptions _options;

        public AiBenchmarkResult? CapturedResult { get; private set; }

        public BenchmarkInterceptingChatClient(
            IChatClient innerClient,
            AiBenchmarkSession session,
            string scenarioName,
            BenchmarkExecutionOptions options)
            : base(innerClient)
        {
            _session = session;
            _scenarioName = scenarioName;
            _options = options;
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (options != null) _options.ChatOptions = options;

            CapturedResult = await _session.BenchmarkAsync(
                _scenarioName,
                InnerClient,
                chatMessages,
                _options,
                cancellationToken).ConfigureAwait(false);

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, CapturedResult.ResponseText))
            {
                ModelId = CapturedResult.ModelId,
                Usage = new UsageDetails
                {
                    InputTokenCount = (int)CapturedResult.InputTokens,
                    OutputTokenCount = (int)CapturedResult.OutputTokens
                }
            };
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (options != null) _options.ChatOptions = options;
            _options.IsStreaming = true;

            CapturedResult = await _session.BenchmarkAsync(
                _scenarioName,
                InnerClient,
                chatMessages,
                _options,
                cancellationToken).ConfigureAwait(false);

            yield return new ChatResponseUpdate
            {
                Contents = new List<AIContent> { new TextContent(CapturedResult.ResponseText) },
                Role = ChatRole.Assistant
            };
        }
    }
}
