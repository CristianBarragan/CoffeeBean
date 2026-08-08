using System;

namespace Graphgine.Mapping;

/// <summary>
/// Assembly-level attribute carrying the full set of EF-derived FK edges,
/// serialized as a delimited string. Declared once, here, in a normally-
/// referenced (non-analyzer) assembly so every project that references
/// Graphgine — whether or not it also runs
/// EntityForeignKeyEmitterGenerator as an analyzer — resolves to the SAME
/// type symbol. Previously this type was (re)declared via
/// RegisterPostInitializationOutput inside the generator itself, which
/// meant every project running the generator as an analyzer got its own,
/// distinct copy of the type — causing CS0436 ambiguous-type warnings and,
/// worse, silent lookup failures in EntityForeignKeyGraph.Build (which
/// compares AttributeClass via SymbolEqualityComparer.Default and found no
/// match between the two distinct symbols).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class EntityForeignKeyGraphAttribute : Attribute
{
    public string Edges { get; }

    public EntityForeignKeyGraphAttribute(string edges)
    {
        Edges = edges;
    }
}