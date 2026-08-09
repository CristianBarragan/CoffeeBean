using Graphgine.Mapping;
using Domain.Model;

namespace Api.Banking.Mapping;

/// <summary>
/// Maps Domain.Model.Wrapper — the query/mutation root, with no backing
/// entity at all. Ported from legacy Domain.Shared/Mapping/WrapperMapping.cs.
///
/// This is deliberately closer to empty than any other mapping in this
/// folder. Two things depend on it existing at all, and neither needs
/// more than `Model = typeof(Wrapper)`:
///
///   - WrapperRootModelResolver scans the syntax tree directly for a
///     class literally named "Wrapper" and treats every one of its
///     non-scalar properties (here, just CustomerCustomerEdge) as a root
///     entity type — that scan doesn't go through IMappingDefinition at
///     all, so this class isn't what satisfies it.
///   - ModelChildrenInference, which DOES need an IMappingDefinition to
///     run against, is what turns Wrapper.CustomerCustomerEdge into a
///     ModelChild automatically — any non-scalar property on a mapped
///     model becomes a ModelChild unless already declared, and this is
///     the one property Wrapper has that qualifies.
///
/// Wrapper.CacheKey (string) and Wrapper.Model (the Model enum used to
/// pick which root query/mutation ran) have nothing to map to — there's
/// no backing entity for either scalar field, so no Entities/Fields are
/// declared here at all. Both are presumably populated by resolver code
/// directly rather than by generated field mapping; that's consistent
/// with legacy's own WrapperMapping, which likewise declared no field
/// maps for either property.
/// </summary>
public sealed class WrapperMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Wrapper)
    };
}
