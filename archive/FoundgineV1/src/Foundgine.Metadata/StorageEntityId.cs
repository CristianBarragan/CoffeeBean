namespace Foundgine.Metadata;

/// <summary>
/// Identifies a physical storage entity (one row in a database table / one
/// vertex label / one AGE label — whatever the provider's "table" concept
/// is), scoped to that physical shape. This is deliberately a different
/// numbering space from <see cref="ModelId"/>: a single logical Model can
/// be backed by more than one storage entity (a composite model — see
/// <see cref="ModelEntityBinding"/>), and a single storage entity can in
/// principle be referenced by more than one Model, so the two identities
/// must never be assumed equal or interchangeable.
///
/// Previously this concept and the model-scoped identity were both called
/// "EntityId", which made it easy for translation code to quietly conflate
/// them (see QueryPlanTranslator's original remarks). Use
/// <see cref="EntityMetadata.OwningModel"/> to go from a storage entity
/// back to the Model that owns it, rather than assuming the two ids line
/// up.
/// </summary>
public readonly record struct StorageEntityId(ushort Value);
