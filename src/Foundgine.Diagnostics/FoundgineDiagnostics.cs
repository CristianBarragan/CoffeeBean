using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Foundgine.Diagnostics;

/// <summary>
/// Foundgine's observability surface: an ActivitySource for tracing
/// and a Meter for metrics, both standard .NET types that any OpenTelemetry
/// (or other System.Diagnostics-based) listener picks up automatically --
/// no custom event/listener plumbing needed, and no cost/behavior change
/// for a consumer who isn't listening. This replaces an earlier
/// hand-rolled DiagnosticEvent/DiagnosticListener/DiagnosticScope scaffold
/// that was never wired to anything; ActivitySource/Meter are the
/// established .NET primitives for exactly this, so building a parallel
/// custom system alongside them would only add surface for no benefit.
///
/// Spans are placed around high-level pipeline stages only (Planner,
/// SQL Generation, Provider Execution, Materialization, and the two
/// top-level Execute Query/Execute Mutation spans that contain them) --
/// deliberately not around every internal helper method, which would add
/// tracing overhead and noise without adding diagnostic value.
/// </summary>
public static class FoundgineDiagnostics
{
    public const string SourceName = "Foundgine";

    public static readonly ActivitySource ActivitySource =
        new(SourceName);

    public static readonly Meter Meter =
        new(SourceName);

    // ---- Metrics -----------------------------------------------------
    // Deliberately small: duration + counts per operation kind, plus plan
    // reuse (only meaningful once plan caching exists) and materialization
    // time as its own histogram, since it's often the dominant cost for a
    // large result set and worth seeing separately from total duration.

    public static readonly Histogram<double> ExecutionDuration =
        Meter.CreateHistogram<double>(
            "foundgine.execution.duration",
            unit: "ms",
            description: "Duration of a query or mutation execution.");

    public static readonly Counter<long> QueriesExecuted =
        Meter.CreateCounter<long>(
            "foundgine.queries.executed",
            description: "Number of queries executed.");

    public static readonly Counter<long> MutationsExecuted =
        Meter.CreateCounter<long>(
            "foundgine.mutations.executed",
            description: "Number of mutations executed.");

    public static readonly Counter<long> PlansReused =
        Meter.CreateCounter<long>(
            "foundgine.plans.reused",
            description: "Number of times a cached execution plan was " +
                          "reused instead of rebuilt. Zero until plan " +
                          "caching exists -- present now so a future " +
                          "cache doesn't need a new metric wired through " +
                          "every call site that could report it.");

    public static readonly Histogram<double> MaterializationDuration =
        Meter.CreateHistogram<double>(
            "foundgine.materialization.duration",
            unit: "ms",
            description: "Time spent turning raw provider rows into " +
                          "typed models, as its own measurement since " +
                          "it's often the dominant cost for a large " +
                          "result set.");
}
