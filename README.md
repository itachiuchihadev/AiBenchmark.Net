# AiBenchmark.Net

[![NuGet](https://img.shields.io/nuget/v/AiBenchmark.Net.svg)](https://www.nuget.org/packages/AiBenchmark.Net/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-purple.svg)](https://dotnet.microsoft.com/)

**AiBenchmark.Net** is a high-performance, declarative LLM benchmarking, metric assertion, and guardrail framework for .NET applications built on [`Microsoft.Extensions.AI`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai).

Just like **BenchmarkDotNet**, AiBenchmark.Net allows you to define benchmarks declaratively using attributes (`[AiBenchmark]`, `[AiArguments]`, `[AiParams]`) alongside fluent SLA assertions (`[AiAssertCost]`, `[AiAssertDuration]`, `[AiAssertResponseContains]`, `[AiAssertToolCalled]`, `[AiAssertSubagentInvoked]`). It measures Time-To-First-Token (TTFT), token consumption, and dollar spend, provides zero-cost offline prompt tokenizer sizing, and **autonomously compiles clean, executive-ready light-mode HTML dashboards**.

---

## 🚀 Key Capabilities

- **Declarative Attribute-Based Benchmarking:** Annotate methods with `[AiBenchmark]` and classes with `[AiBenchmarkSuite]`, mirroring the familiar BenchmarkDotNet ergonomics.
- **Multiple Scenarios via `[AiArguments]`:** Add multiple `[AiArguments(param1, param2, ...)]` attributes to run the same benchmark method across diverse prompts and scenarios.
- **Model & Hyperparameter Permutations via `[AiParams]`:** Test matrix permutations across multiple models or hyperparameters automatically.
- **Declarative Guardrail Assertions:**
  - **Cost Limits:** `[AiAssertCost(maxCostUsd: 0.0001)]`
  - **Latency & Streaming SLAs:** `[AiAssertDuration(maxDurationMs: 3000)]`, `[AiAssertTimeToFirstToken(maxTtftMs: 1200)]`
  - **Token Ceilings:** `[AiAssertTokens(maxInput: 100, maxOutput: 150)]`
  - **Response Validation:** `[AiAssertResponseContains("keyword")]`, `[AiAssertValidJson]`, `[AiAssertResponseMatches("regex")]`
  - **Tool & MCP Guardrails:** `[AiAssertToolCalled("tool_name")]`, `[AiAssertMcpToolCalled("server", "tool")]`
  - **Multi-Agent Sequences:** `[AiAssertSubagentInvoked("researcher")]`, `[AiAssertDelegationSequence("researcher", "coder")]`
- **Zero-Cost Offline Evaluation (Zero API Calls):** Evaluate prompt sizes and cost budgets offline locally in `<1ms` without calling remote LLM APIs (`Offline = true`).
- **Autonomous Light-Mode HTML Reports:** Automatically compiles and opens a clean executive dashboard (`Inter` + `JetBrains Mono`, KPI cards, assertion pass rates, criteria filters) upon suite completion.
- **Multi-Targeting & Version-Specific Optimizations:** Tailored for `.NET 8.0`, `.NET 9.0`, and `.NET 10.0` with version-specific primitives.

---

## ⚡ Multi-Targeting & .NET Optimizations

AiBenchmark.Net multi-targets all modern .NET releases supported by `Microsoft.Extensions.AI`:
- **`net8.0`**: Utilizes `System.Collections.Frozen.FrozenDictionary<string, ModelPricingInfo>` for SIMD-accelerated, zero-allocation model catalog lookups. Performs response assertion substring searches using zero-allocation `ReadOnlySpan<char>` slicing.
- **`net9.0`**: Leverages .NET 9's dedicated `System.Threading.Lock` primitive (`System.Threading.Lock` instead of legacy `object` monitors) for ultra-fast, thread-safe session metrics aggregation.
- **`net10.0`**: Leverages .NET 10 performance runtime optimizations, modern C# language features, and native AOT readiness.

---

## 📦 Installation

```bash
dotnet add package AiBenchmark.Net
```

Compatible with **.NET 8.0**, **.NET 9.0**, and **.NET 10.0+**.

---

## ⚡ Declarative Benchmarking (BenchmarkDotNet Style)

### 1. Define a Benchmark Class

```csharp
using System.Threading.Tasks;
using AiBenchmark.Net.Benchmarking;
using Microsoft.Extensions.AI;

[AiBenchmarkSuite("Customer Support Quality & Latency SLA")]
public class CustomerSupportBenchmarks
{
    // Parameter permutations (similar to BenchmarkDotNet's [Params])
    [AiParams("gemini-3.5-flash-lite")]
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    // Scenario with multiple parameter sets & dynamic placeholder assertions
    [AiBenchmark("Intent Classification & Routing SLA")]
    [AiArguments("I want to cancel my subscription and get a refund", "refund")]
    [AiArguments("My screen went black and won't turn on", "hardware")]
    [AiAssertCost(maxCostUsd: 0.0001)]
    [AiAssertDuration(maxDurationMs: 4000)]
    [AiAssertResponseContains("{1}")] // Evaluates against argument at index 1
    public async Task<string> ClassifyIntent(string query, string expectedCategory, IChatClient client)
    {
        var response = await client.GetResponseAsync($"Classify query into refund or hardware: '{query}'. Respond in one word.");
        return response.Text ?? string.Empty;
    }

    // Streaming latency & Time-To-First-Token (TTFT) assertion
    [AiBenchmark("Streaming Haiku TTFT SLA", IsStreaming = true)]
    [AiAssertTimeToFirstToken(maxTtftMs: 1500)]
    [AiAssertDuration(maxDurationMs: 5000)]
    [AiAssertCost(maxCostUsd: 0.00005)]
    public string StreamingHaiku()
    {
        return "Write a 3-line haiku about automated testing and token budgets.";
    }

    // Zero-Cost Offline Evaluation (0 remote network calls, tokenizer sizing in <1ms)
    [AiBenchmark("Contract Sizing Budget", Offline = true)]
    [AiArguments("SYSTEM: Legal analyzer.\nClause 4.2: Confidentiality.\nUSER: Verify.")]
    [AiAssertTokens(maxInput: 300)]
    [AiAssertCost(maxCostUsd: 0.00005)]
    public string OfflinePromptAnalysis(string promptText)
    {
        return promptText;
    }
}
```

### 2. Run the Benchmark Suite

```csharp
// Execute all benchmarks, display console summary table, and auto-publish HTML report
var summary = await AiBenchmarkRunner.Run<CustomerSupportBenchmarks>(chatClient);
```

### 3. Console Output (BenchmarkDotNet Style)

```
// * Summary: Customer Support Quality & Latency SLA *
|------------------------------------------|-----------|------------|-----------|--------------|------------|--------------|
| Method / Scenario                        | In Tokens | Out Tokens |      TTFT | Mean Latency |   Cost ($) |   Assertions |
|------------------------------------------|-----------|------------|-----------|--------------|------------|--------------|
| Intent Classification & Routing SLA (... |        26 |          2 |         - |       491 ms |  $0.000004 |     4/4 PASS |
| Intent Classification & Routing SLA (... |        24 |          2 |         - |       466 ms |  $0.000003 |     4/4 PASS |
| Streaming Haiku TTFT SLA (Model=gemi...  |        16 |         19 |   1052 ms |      1139 ms |  $0.000007 |     3/3 PASS |
| Contract Sizing Budget (Model=gemini...  |        32 |          0 |         - |         0 ms |  $0.000002 |     2/2 PASS |
|------------------------------------------|-----------|------------|-----------|--------------|------------|--------------|

[AiBenchmark.Net] Auto-generated Benchmark Report: ./AiBenchmarkReports/Benchmark_Customer_Support_Quality_20260927.html
```

---

## 🛠️ Advanced Guardrails: Tools, MCP & Subagents

### Function Calling & Tool Guardrails

```csharp
[AiBenchmark("Flight & Weather Multi-Tool Agent")]
[AiAssertToolCalled("get_weather_forecast")]
[AiAssertToolCalled("currency_converter")]
[AiAssertToolCallCount(2)]
public async Task<string> MultiToolAgent(IChatClient client, BenchmarkExecutionOptions options)
{
    options.ToolInvocations.Add(new ToolInvocationRecord
    {
        ToolName = "get_weather_forecast",
        Arguments = new Dictionary<string, object?> { ["location"] = "Tokyo" },
        Success = true
    });
    options.ToolInvocations.Add(new ToolInvocationRecord
    {
        ToolName = "currency_converter",
        Arguments = new Dictionary<string, object?> { ["from"] = "USD", ["to"] = "JPY" },
        Success = true
    });

    var response = await client.GetResponseAsync("What is the weather in Tokyo and $100 in JPY?");
    return response.Text ?? string.Empty;
}
```

### Model Context Protocol (MCP) Guardrails

```csharp
[AiBenchmark("MCP Pull Request Workflow")]
[AiAssertMcpToolCalled("github", "create_pull_request")]
[AiAssertMcpResourceAccessed("mcp://enterprise/kb/architecture_specs")]
[AiAssertMcpServerCount("github", maxCalls: 2)]
public async Task<string> McpWorkflow(IChatClient client, BenchmarkExecutionOptions options)
{
    options.McpInvocations.Add(new McpInvocationRecord
    {
        ServerName = "github",
        ToolName = "create_pull_request",
        Success = true
    });
    options.McpInvocations.Add(new McpInvocationRecord
    {
        ServerName = "filesystem",
        ResourceUri = "mcp://enterprise/kb/architecture_specs",
        Success = true
    });

    var response = await client.GetResponseAsync("Create a pull request for the feature.");
    return response.Text ?? string.Empty;
}
```

### Hierarchical Subagent Chains

```csharp
[AiBenchmark("Hierarchical Subagent Chain")]
[AiAssertSubagentInvoked("researcher_agent")]
[AiAssertSubagentCount(3)]
[AiAssertDelegationSequence("researcher_agent", "coder_agent", "synthesizer_agent")]
public async Task<string> SubagentDelegation(IChatClient client, BenchmarkExecutionOptions options)
{
    options.SubagentInvocations.Add(new SubagentInvocationRecord { AgentRole = "researcher_agent" });
    options.SubagentInvocations.Add(new SubagentInvocationRecord { AgentRole = "coder_agent" });
    options.SubagentInvocations.Add(new SubagentInvocationRecord { AgentRole = "synthesizer_agent" });

    var response = await client.GetResponseAsync("Coordinate research, implementation, and summary.");
    return response.Text ?? string.Empty;
}
```

---

## 📊 Autonomous Light-Mode HTML Reports

When a benchmark run completes, `AiBenchmark.Net` automatically compiles and publishes a clean, executive-ready HTML dashboard:

- **Clean Typography:** Built with Google Fonts `Inter` and `JetBrains Mono`. Zero dark-mode/neon artifacts.
- **Executive KPI Cards:** Total Scenarios, Pass/Fail Rates, Total Token Usage, and Suite Spend ($ USD).
- **Interactive Criteria Filters:** Filter by `All Invocations`, `Streaming Only`, `Non-Streaming`, `Tools / MCP`, `Failed Assertions`, or `Errors`.
- **Search & Deep Inspection:** Real-time client-side search with expandable drawers showing prompt text, full model responses, tool payloads, MCP resources, subagent sequences, and individual assertion outcomes.

---

## 🧪 Real-World Verification with `TestPacakge`

The repository includes a dedicated test application in `TestPacakge/` that verifies 10 scenarios across multiple `[AiArguments]`, streaming TTFT, offline evaluation, tools, MCP, and subagents against live **Google Gemini (`gemini-3.5-flash-lite`)**:

```bash
# 1. Set your Gemini API Key
export GEMINI_API_KEY="AIza..."

# 2. Run the declarative benchmark suite (supports net8.0, net9.0, net10.0)
dotnet run --project TestPacakge/TestPacakge.csproj -f net9.0
```

The suite validates:
1. `[AiBenchmark]` with multiple `[AiArguments]` parameter sets & dynamic assertion placeholders (`{1}`).
2. Real streaming TTFT SLA verification.
3. Zero-cost offline prompt tokenizer sizing (<1ms, 0 network calls).
4. Multi-tool function calling guardrails.
5. Model Context Protocol (MCP) server & resource assertions.
6. Subagent delegation order & spend assertions.
7. Declarative prompt verification matrix.
8. BenchmarkDotNet-style summary table in the console.
9. Autonomous compilation of the executive HTML dashboard.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
