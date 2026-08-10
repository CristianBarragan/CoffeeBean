namespace Foundgine.Foundation;
public readonly record struct Optional<T>(bool HasValue,T? Value);
