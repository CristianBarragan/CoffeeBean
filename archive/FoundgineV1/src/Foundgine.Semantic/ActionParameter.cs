namespace Foundgine.Semantic;

/// <summary>One named input an <see cref="ActionDescriptor"/> accepts.</summary>
public sealed record ActionParameter(string Name, Type ClrType, bool IsRequired = true);
