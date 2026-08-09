using Graphgine.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Api.Banking.Mapping;

/// <summary>
/// Maps Domain.Model.Transaction onto Database.Entity.Transaction.
/// Ported from legacy Domain.Shared/Mapping/TransactionMapping.cs.
///
/// Legacy hand-declared both parent joins (Transaction -> Contract via
/// ContractId/ContractKey, Transaction -> Account via AccountId/AccountKey).
/// Both are already declared as EF Fluent FK edges from the PARENT side —
/// AccountEntityConfiguration.HasMany(Transaction).HasForeignKey(AccountId)
/// and ContractEntityConfiguration.HasMany(Transaction).HasForeignKey(ContractId)
/// — so EntityNavigationConvention resolves Transaction.Account and
/// Transaction.Contract automatically once Account's and Contract's own
/// mappings exist; FluentEntityNavigationConvention's FK-graph builder
/// treats HasMany(...).WithOne(...).HasForeignKey(...) the same as the
/// HasOne(...).WithMany(...) form.
///
/// AccountKey/ContractKey scalars on Transaction: Database.Entity.Transaction
/// carries both AccountKey and ContractKey (Guid?, mirroring the natural
/// keys directly) alongside the int AccountId/ContractId FKs — unlike the
/// ContactPoint/Contract gaps flagged elsewhere in this port, these match
/// Domain.Model.Transaction's own AccountKey/ContractKey by name and type,
/// so they're left to convention rather than declared explicitly.
/// </summary>
public sealed class TransactionMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Transaction),
        Schema = nameof(DataEntity.Schema.Lending),

        Entity = typeof(DataEntity.Transaction),
        Key = nameof(DataEntity.Transaction.TransactionKey)

        // Amount/Balance/AccountKey/ContractKey all match by name+type —
        // left to convention. Account/Contract navigations — left to
        // convention, see header comment.
    };
}
