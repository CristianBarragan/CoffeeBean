using Graphgine.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Api.Banking.Mapping;

/// <summary>
/// Maps Domain.Model.Contract onto Database.Entity.Contract.
/// Ported from legacy Domain.Shared/Mapping/ContractMapping.cs.
///
/// Legacy hand-declared three joins by hand (Contract -> Account,
/// Contract -> CustomerBankingRelationship, Contract -> Transaction).
/// All three already exist as EF Fluent FK declarations reachable from
/// Contract's own entity (ContractEntityConfiguration.HasMany(Transaction),
/// AccountEntityConfiguration.HasOne(Contract).HasForeignKey&lt;Contract&gt;,
/// and Contract's own CustomerBankingRelationshipId column matched against
/// CustomerBankingRelationshipEntityConfiguration.HasMany(Contract)) — so
/// Account/Transaction resolve automatically once those two mappings
/// exist too. Contract.CustomerBankingRelationshipKey is a plain scalar
/// on the new Domain.Model.Contract (not a navigation, unlike the legacy
/// model), and matches Entity.Contract's own CustomerBankingRelationshipId
/// only in intent, not in name/type — there's no CustomerBankingRelationshipKey
/// column on Entity.Contract at all, only CustomerBankingRelationshipId
/// (int). Left unmapped for the same reason as ContactPoint.CustomerKey:
/// no compatible column to bind it to without a schema change. See
/// PORT-STATUS.md.
///
/// Legacy's field map also had a curious cross-model entry (Product's OLD
/// ProductType feeding Contract.ContractType via a shared enum table) —
/// that was an artifact of the legacy row-sharing model. The new
/// Domain.Model.Contract has its own ContractType property directly, so
/// this port maps it straightforwardly instead; the equivalent
/// ProductType -> ContractType cross-mapping lives on ProductMapping.cs,
/// where Product actually needs it (Product has no ContractType of its
/// own — only ProductType, which writes through to Contract's row).
/// </summary>
public sealed class ContractMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Contract),
        Schema = nameof(DataEntity.Schema.Lending),

        Entity = typeof(DataEntity.Contract),
        Key = nameof(DataEntity.Contract.ContractKey),

        Fields =
        [
            // Same member names both sides (CreditCard/Mortgage/PersonalLoan).
            new FieldDefinition
            {
                Source = nameof(Contract.ContractType),
                Entity = typeof(DataEntity.Contract),
                Destination = nameof(DataEntity.Contract.ContractType),
                EnumMapping = new EnumMappingDefinition<ContractType, DataEntity.ContractType>()
            }

            // Amount matches by name+type — left to convention.
            // CustomerBankingRelationshipKey is intentionally NOT mapped —
            // see header comment.
        ]
    };
}
