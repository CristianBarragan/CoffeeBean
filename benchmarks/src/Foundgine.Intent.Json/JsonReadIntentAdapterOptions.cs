namespace Foundgine.Intent.Json;

/// <summary>
/// Bounds applied while parsing untrusted structured intent. These limits are
/// intentionally enforced at the protocol boundary before semantic resolution.
/// </summary>
public sealed record JsonReadIntentAdapterOptions
{
    public int MaxSelectionDepth { get; init; } = 32;
    public int MaxSelections { get; init; } = 256;
    public int MaxFilterDepth { get; init; } = 32;
    public int MaxFilterNodes { get; init; } = 256;
    public int MaxJsonValueDepth { get; init; } = 16;
}
