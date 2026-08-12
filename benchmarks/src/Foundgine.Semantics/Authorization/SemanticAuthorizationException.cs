namespace Foundgine.Semantics.Authorization;

public sealed class SemanticAuthorizationException(string message) : InvalidOperationException(message);
