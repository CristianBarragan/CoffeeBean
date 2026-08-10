using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Foundgine.Providers;
using Foundgine.Semantic.Intent;

namespace Foundgine.Tests;

/// <summary>
/// Milestone 3's closing ask, kept exactly as small as the architecture
/// review recommended: "Don't build a giant evidence framework. Just make
/// one end-to-end record." This answers the review's own four questions --
///
/// <list type="bullet">
/// <item><description>Who did we resolve, and why? -- <see cref="Resolution"/>.</description></item>
/// <item><description>What plan did we generate? -- <see cref="Plan"/>, the real SQL <see cref="Foundgine.Providers.SqlTextTranslator"/> compiled.</description></item>
/// <item><description>What provider executed it? -- <see cref="Execution"/>.</description></item>
/// <item><description>What data came back? -- <see cref="Result"/>.</description></item>
/// </list>
///
/// by stitching together output that <c>Foundgine.Semantic.Resolution</c>,
/// <c>Foundgine.Providers</c>, and <c>Foundgine.Execution.Contracts</c>
/// already produce -- nothing new added to any of those projects, and no
/// new project for this to live in. If Evidence needs to grow into
/// something Foundgine.* owns as a first-class concept later (Milestone 7
/// in the roadmap), this is deliberately small enough to throw away and
/// redo rather than a foundation to extend carefully.
/// </summary>
public sealed record ReadEvidence(
    IReadOnlyList<string> Resolution,
    string Plan,
    string Execution,
    IReadOnlyList<string> Result)
{
    public static ReadEvidence Build(
        ResolvedReadPlan readPlan,
        SqlTranslation translation,
        ProviderKind providerKind,
        IReadOnlyList<ExecutionRow> rows,
        EntityId resultEntity)
    {
        var resolution = readPlan.Evidence
            .Select(e => e.Description)
            .ToArray();

        var plan = translation.Parameters.Count == 0
            ? translation.CommandText
            : translation.CommandText + " -- params: " +
              string.Join(", ", translation.Parameters.Select(p => $"{p.Name}={p.Value}"));

        var execution = $"{providerKind} provider, {rows.Count} row(s) returned";

        var result = rows
            .Select(row => string.Join(", ", row.Single(resultEntity)))
            .ToArray();

        return new ReadEvidence(resolution, plan, execution, result);
    }
}
