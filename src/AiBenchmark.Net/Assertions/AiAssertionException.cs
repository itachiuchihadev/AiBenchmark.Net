using System;
using AiBenchmark.Net.Models;

namespace AiBenchmark.Net.Assertions;

/// <summary>
/// Exception thrown when an AI benchmark or evaluation assertion condition fails.
/// </summary>
public sealed class AiAssertionException : Exception
{
    /// <summary>The failed assertion details.</summary>
    public AssertionResult Assertion { get; }

    /// <summary>The invocation report that caused the failure.</summary>
    public AiBenchmarkReport? Report { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AiAssertionException"/>.
    /// </summary>
    public AiAssertionException(AssertionResult assertion, AiBenchmarkReport? report = null)
        : base(FormatMessage(assertion))
    {
        Assertion = assertion;
        Report = report;
    }

    private static string FormatMessage(AssertionResult assertion)
    {
        return $"[{assertion.Category} Assertion Failed] {assertion.AssertionName}\n" +
               $"  Expected: {assertion.ExpectedValue}\n" +
               $"  Actual:   {assertion.ActualValue}\n" +
               $"  Details:  {assertion.FailureMessage}";
    }
}
