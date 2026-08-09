using Graphgine.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Api.Banking.Mapping;

/// <summary>
/// Maps Domain.Model.CustomerBankingRelationship onto
/// Database.Entity.CustomerBankingRelationship. Ported from legacy
/// Domain.Shared/Mapping/CustomerBankingRelationshipMapping.cs.
///
/// Legacy hand-declared the Customer parent join and the Contract child
/// join; both already exist as EF Fluent FK declarations
/// (CustomerEntityConfiguration.HasMany(CustomerBankingRelationship)
/// .HasForeignKey(CustomerId), and
/// CustomerBankingRelationshipEntityConfiguration.HasMany(Contract)
/// .HasForeignKey(CustomerBankingRelationshipId)), so both resolve via
/// convention once Customer's and Contract's own mappings exist.
///
/// -----------------------------------------------------------------------
/// FLAGGED — Domain.Model.CustomerBankingRelationship carries a property
/// (CustomerCustomerRelationshipType) that has no equivalent on
/// Database.Entity.CustomerBankingRelationship at all — that enum lives on
/// CustomerCustomerRelationship/CustomerCustomerEdge, not here. Reads as a
/// copy/paste leftover on the domain model rather than something this
/// mapping should paper over. Convention field-matching will report a
/// NoMatchingProperty diagnostic for it (non-fatal, but worth cleaning up
/// on the model — see PORT-STATUS.md) — no explicit Field is added for it
/// here since there's nothing correct to point it at.
/// </summary>
public sealed class CustomerBankingRelationshipMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(CustomerBankingRelationship),
        Schema = nameof(DataEntity.Schema.Banking),

        Entity = typeof(DataEntity.CustomerBankingRelationship),
        Key = nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)

        // CustomerKey matches by name+type (Guid? on both sides) — left to
        // convention. Contract navigation — left to convention, see
        // header comment. CustomerCustomerRelationshipType intentionally
        // left unmapped — see header comment.
    };
}
