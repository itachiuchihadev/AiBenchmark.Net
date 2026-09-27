using System;
using System.IO;
using System.Threading.Tasks;
using AiBenchmark.Net.Benchmarking;

namespace TestPacakge;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔═════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        AiBenchmark.Net — Declarative Attribute-Based Benchmark Runner       ║");
        Console.WriteLine("║   [AiBenchmarkSuite], [AiParams], [AiBenchmark], [AiArguments], [AiAssert]  ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        // 1. Resolve GEMINI_API_KEY from environment
        string? apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ ERROR: GEMINI_API_KEY environment variable is not set!");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        string maskedKey = apiKey.Length > 8 ? $"{apiKey[..4]}...{apiKey[^4..]}" : "****";
        Console.WriteLine($"[CONFIG] GEMINI_API_KEY: {maskedKey}");
        Console.WriteLine($"[CONFIG] Target Model: gemini-3.5-flash-lite");
        Console.ResetColor();
        Console.WriteLine();

        // 2. Initialize real GeminiChatClient
        const string targetModel = "gemini-3.5-flash-lite";
        var geminiClient = new GeminiChatClient(apiKey: apiKey, modelId: targetModel);

        // 3. Run BenchmarkDotNet-style declarative benchmark class
        string reportsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "AiBenchmarkReports");

        var summary = await AiBenchmarkRunner.Run<GeminiLiveBenchmarks>(geminiClient, session =>
        {
            session.ReportDirectory = reportsDirectory;
            session.AutoGenerateHtmlReport = true;
            session.AutoOpenReport = true;
        });

        // 4. Print Suite Results Summary
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔═════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                 BENCHMARK SUITE EXECUTION SUMMARY                           ║");
        Console.WriteLine("╠═════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Total Benchmark Scenarios Run:          {summary.TotalScenarios,42} ║");
        Console.WriteLine($"║ Total Assertions Evaluated:             {summary.TotalAssertions,42} ║");
        Console.WriteLine($"║ Total Assertions Passed:                {summary.PassedAssertions,42} ({100.0 * summary.PassedAssertions / Math.Max(1, summary.TotalAssertions):F0}%) ║");
        Console.WriteLine($"║ Total Tokens Tracked:                   {summary.TotalTokens,42:N0} ║");
        Console.WriteLine($"║ Total Suite Cost (USD):                 ${summary.TotalCostUsd,41:F6} ║");
        Console.WriteLine($"║ Suite Duration:                         {summary.TotalExecutionDuration.TotalSeconds,39:F1}s ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();

        if (!summary.AllAssertionsPassed)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Benchmark suite completed with {summary.FailedAssertions} failed assertions.");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n✨ All benchmark scenarios and assertion guardrails passed successfully!");
        Console.ResetColor();

        return 0;
    }
}
