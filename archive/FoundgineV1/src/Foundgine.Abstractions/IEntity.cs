namespace Foundgine.Abstractions;

/// <summary>Represents a domain entity with a strongly typed identity.</summary>
public interface IEntity<out TId> where TId : notnull
{
    TId Id { get; }
}
