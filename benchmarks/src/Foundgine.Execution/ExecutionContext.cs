namespace Foundgine.Execution;

/// <summary>Runtime values supplied to an already-planned execution.
/// Semantic planning remains independent of these values.</summary>
public sealed record ExecutionContext(
    IReadOnlyDictionary<string, object?>? Values = null)
{
    public IReadOnlyDictionary<string, object?> EffectiveValues => Values ?? EmptyValues;

    private static readonly IReadOnlyDictionary<string, object?> EmptyValues =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    public bool TryGetValue(string path, out object? value) =>
        EffectiveValues.TryGetValue(path, out value);
}
