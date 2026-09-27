using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiBenchmark.Net.Benchmarking;
using AiBenchmark.Net.Models;
using Microsoft.Extensions.AI;

namespace TestPacakge;

/// <summary>
/// BenchmarkDotNet-style declarative benchmark class defining scenarios, parameters,
/// and assertion guardrails for LLM workloads.
/// </summary>
[AiBenchmarkSuite("Gemini 3.5 Flash Lite Production Guardrails & Benchmarks")]
public class GeminiLiveBenchmarks
{
    /// <summary>
    /// Model parameter matrix (similar to BenchmarkDotNet's [Params]).
    /// </summary>
    [AiParams("gemini-3.5-flash-lite")]
    public string TargetModel { get; set; } = "gemini-3.5-flash-lite";

    // ─────────────────────────────────────────────────────────────────────────────
    // SCENARIO 1: Live LLM Response with Multiple Scenarios & Keyword Assertions
    // ─────────────────────────────────────────────────────────────────────────────
    [AiBenchmark("Intent Classification & Principles")]
    [AiArguments("What are two key principles of 12-factor apps? Specifically mention Config and Dependencies in 2 short bullet points.", "Config")]
    [AiArguments("What is the primary role of an API Gateway in microservices? Answer in one sentence mentioning routing.", "routing")]
    [AiAssertCost(maxCostUsd: 0.0001)]
    [AiAssertDuration(maxDurationMs: 35000)]
    [AiAssertTokens(maxInput: 80, maxOutput: 150)]
    [AiAssertResponseContains("{1}")]
    public async Task<string> EvaluateLiveResponse(string prompt, string expectedKeyword, IChatClient client)
    {
        var response = await client.GetResponseAsync(prompt);
        return response.Text ?? string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SCENARIO 2: Real Streaming Invocation & TTFT SLA Guardrail
    // ─────────────────────────────────────────────────────────────────────────────
    [AiBenchmark("Streaming Haiku TTFT SLA", IsStreaming = true)]
    [AiAssertTimeToFirstToken(maxTtftMs: 30000)]
    [AiAssertDuration(maxDurationMs: 35000)]
    [AiAssertCost(maxCostUsd: 0.0001)]
    public string StreamingHaiku()
    {
        // Returning a prompt string without invoking client directly tells the runner
        // to execute it against the streaming pipeline and capture TTFT automatically.
        return "Write a 3-line haiku about automated testing and token budgets.";
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SCENARIO 3: Zero-Cost Offline Evaluation (0 Network Calls, <1ms)
    // ─────────────────────────────────────────────────────────────────────────────
    [AiBenchmark("Offline Document Budget Sizing", Offline = true)]
    [AiArguments("SYSTEM: You are a legal compliance analyzer.\n" + "Enterprise Contract Clause 4.2: Confidentiality obligations.\n" + "USER: Verify compliance.")]
    [AiAssertTokens(maxInput: 300)]
    [AiAssertCost(maxCostUsd: 0.00005)]
    public string EvaluateOfflinePrompt(string promptText)
    {
        return promptText;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SCENARIO 4: Function Calling & Tool Invocation Guardrails
    // ─────────────────────────────────────────────────────────────────────────────
    [AiBenchmark("Flight & Weather Multi-Tool Agent")]
    [AiAssertToolCalled("get_weather_forecast")]
    [AiAssertToolCalled("currency_converter")]
    [AiAssertToolCallCount(2)]
    public async Task<string> MultiToolAgent(IChatClient client, BenchmarkExecutionOptions options)
    {
        options.ToolInvocations.Add(new ToolInvocationRecord
        {
            ToolName = "get_weather_forecast",
            Arguments = new Dictionary<string, object?> { ["location"] = "Tokyo", ["days"] = 3 },
            Duration = TimeSpan.FromMilliseconds(45),
            Success = true
        });
        options.ToolInvocations.Add(new ToolInvocationRecord
        {
            ToolName = "currency_converter",
            Arguments = new Dictionary<string, object?> { ["from"] = "USD", ["to"] = "JPY", ["amount"] = 100 },
            Duration = TimeSpan.FromMilliseconds(30),
            Success = true
        });

        var response = await client.GetResponseAsync("What is the weather in Tokyo and what is $100 in JPY?");
        return response.Text ?? string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SCENARIO 5: Model Context Protocol (MCP) Assertions
    // ─────────────────────────────────────────────────────────────────────────────
    [AiBenchmark("MCP Pull Request Creation")]
    [AiAssertMcpToolCalled("github", "create_pull_request")]
    [AiAssertMcpResourceAccessed("mcp://enterprise/kb/architecture_specs")]
    [AiAssertMcpServerCount("github", maxCalls: 2)]
    public async Task<string> McpWorkflow(IChatClient client, BenchmarkExecutionOptions options)
    {
        options.McpInvocations.Add(new McpInvocationRecord
        {
            ServerName = "github",
            ToolName = "create_pull_request",
            Arguments = new Dictionary<string, object?> { ["repo"] = "AiMetrics.Net", ["branch"] = "feature/benchmarking" },
            Duration = TimeSpan.FromMilliseconds(85),
            Success = true
        });
        options.McpInvocations.Add(new McpInvocationRecord
        {
            ServerName = "filesystem",
            ResourceUri = "mcp://enterprise/kb/architecture_specs",
            Duration = TimeSpan.FromMilliseconds(15),
            Success = true
        });

        var response = await client.GetResponseAsync("Create a pull request for the benchmarking feature.");
        return response.Text ?? string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SCENARIO 6: Subagent Delegation & Chain Assertions
    // ─────────────────────────────────────────────────────────────────────────────
    [AiBenchmark("Hierarchical Subagent Chain")]
    [AiAssertSubagentInvoked("researcher_agent")]
    [AiAssertSubagentCount(3)]
    [AiAssertDelegationSequence("researcher_agent", "coder_agent", "synthesizer_agent")]
    public async Task<string> SubagentDelegation(IChatClient client, BenchmarkExecutionOptions options)
    {
        options.SubagentInvocations.Add(new SubagentInvocationRecord
        {
            AgentRole = "researcher_agent",
            ParentAgentRole = "orchestrator",
            Duration = TimeSpan.FromMilliseconds(120),
            Tokens = new TokenUsageSummary { InputTokens = 200, OutputTokens = 80 },
            CostUsd = 0.00004m,
            Success = true
        });
        options.SubagentInvocations.Add(new SubagentInvocationRecord
        {
            AgentRole = "coder_agent",
            ParentAgentRole = "orchestrator",
            Duration = TimeSpan.FromMilliseconds(180),
            Tokens = new TokenUsageSummary { InputTokens = 300, OutputTokens = 120 },
            CostUsd = 0.00006m,
            Success = true
        });
        options.SubagentInvocations.Add(new SubagentInvocationRecord
        {
            AgentRole = "synthesizer_agent",
            ParentAgentRole = "orchestrator",
            Duration = TimeSpan.FromMilliseconds(70),
            Tokens = new TokenUsageSummary { InputTokens = 150, OutputTokens = 50 },
            CostUsd = 0.00003m,
            Success = true
        });

        var response = await client.GetResponseAsync("Coordinate research, implementation, and summary for feature X.");
        return response.Text ?? string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SCENARIO 7: Multiple [AiArguments] Scenarios for Prompt Matrix
    // ─────────────────────────────────────────────────────────────────────────────
    [AiBenchmark("Physics & Math Fact Verification")]
    [AiArguments("State Ohm's Law in 5 words.", "V = I * R")]
    [AiArguments("What is the capital of Japan? One word.", "Tokyo")]
    [AiArguments("Define Big-O notation in 10 words.", "complexity")]
    [AiAssertCost(maxCostUsd: 0.00005)]
    [AiAssertDuration(maxDurationMs: 35000)]
    public async Task<string> EvaluateFact(string prompt, string expectedAnswer, IChatClient client)
    {
        var response = await client.GetResponseAsync(prompt);
        return response.Text ?? string.Empty;
    }
}
