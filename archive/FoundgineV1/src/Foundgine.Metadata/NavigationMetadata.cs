namespace Foundgine.Metadata;

/// <summary>
/// A model-level navigation (e.g. Wrapper.customer), resolved once at
/// generation time by EntityNavigationConvention (the same resolution
/// PlannerEmitter's join emission already uses) and exposed at runtime so
/// filtering/ordering can walk navigation paths without needing their own
/// separate navigation-resolution logic.
/// </summary>
public sealed record NavigationMetadata(
    string Name,
    ModelId TargetModel
);
