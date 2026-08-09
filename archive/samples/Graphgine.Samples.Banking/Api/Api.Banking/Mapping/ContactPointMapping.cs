using Graphgine.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Api.Banking.Mapping;

/// <summary>
/// Maps Domain.Model.ContactPoint onto Database.Entity.ContactPoint.
/// Ported from legacy Domain.Shared/Mapping/ContactPointMapping.cs.
///
/// The legacy version hand-declared both EntityParents joins (Customer.Id
/// and Customer.CustomerKey) — those aren't needed here: the FK edge
/// (ContactPoint.CustomerId -> Customer.Id) is already declared in
/// CustomerEntityConfiguration.HasMany(...).WithOne(...).HasForeignKey(...),
/// so EntityNavigationConvention resolves the parent join on its own once
/// Customer's own mapping exists.
///
/// -----------------------------------------------------------------------
/// FIXED — this was a real schema gap; now resolved
/// -----------------------------------------------------------------------
/// Domain.Model.ContactPoint.CustomerKey is `Guid?`. The backing entity's
/// CustomerKey was previously typed `int?` — not type-compatible under
/// FieldMapGeneration.AreTypesCompatible (only Guid<->string, enum<->numeric,
/// and numeric<->numeric are treated as compatible; Guid<->int is not), so
/// convention field-matching would have failed for this property.
/// Database.Entity.ContactPoint.CustomerKey has been retyped to `Guid?`
/// (mirroring the CustomerKey/AccountKey/ContractKey pattern already used
/// on Transaction, alongside the real int CustomerId FK) — it now matches
/// by name and type, so no explicit Field entry is needed here; left to
/// convention like the rest of this file. This retype requires a new EF
/// migration before the sample can run against a real database — see
/// PORT-STATUS.md.
/// </summary>
public sealed class ContactPointMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(ContactPoint),
        Schema = nameof(DataEntity.Schema.Banking),

        Entity = typeof(DataEntity.ContactPoint),
        Key = nameof(DataEntity.ContactPoint.ContactPointKey),

        Fields =
        [
            // Same member names on both sides (Mobile/Landline/Email), so
            // FieldMapGeneration's Enum<->Enum type-compatibility check
            // would already let this through unmapped. Declared explicitly
            // anyway, matching the legacy mapping's own caution around
            // enum fields, so the exact member-to-member correspondence is
            // stated rather than assumed.
            new FieldDefinition
            {
                Source = nameof(ContactPoint.ContactPointType),
                Entity = typeof(DataEntity.ContactPoint),
                Destination = nameof(DataEntity.ContactPoint.ContactPointType),
                EnumMapping = new EnumMappingDefinition<ContactPointType, DataEntity.ContactPointType>()
            }

            // ContactPointValue and CustomerKey match by name+type — left
            // to convention.
        ]
    };
}
