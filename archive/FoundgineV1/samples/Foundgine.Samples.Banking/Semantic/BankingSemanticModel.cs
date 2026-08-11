using Foundgine.Metadata;
using Foundgine.Samples.Banking.Metadata;
using Foundgine.Semantic;

namespace Foundgine.Samples.Banking.Semantic;

/// <summary>
/// Milestone 1 for the Banking sample: Customer -&gt; Account -&gt; Transaction
/// turned into a protocol-neutral <see cref="SemanticModel"/> on top of
/// the <see cref="BankingMetadata"/> already used for planning/execution.
///
/// Hand-authored, same as <see cref="BankingMetadata"/> itself -- Milestone
/// 1 is explicit that the product proof should not block on a source
/// generator. The <see cref="EntityId"/>s below are the exact same ids
/// <see cref="BankingMetadata"/> registers, so this model and the
/// execution pipeline are describing the same domain, not a parallel one.
/// </summary>
public static class BankingSemanticModel
{
    public static SemanticModel Build() =>
        new SemanticModelBuilder()
            .Entity(BankingMetadata.Customer.EntityId, "Customer", customer => customer
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Name", typeof(string))
                .Relationship(
                    new RelationshipId(1),
                    "Accounts",
                    BankingMetadata.Account.EntityId,
                    RelationshipCardinality.Many)
                .Search(new SearchCapability([new FieldId(2)], SearchStrategy.Fuzzy)))
            .Entity(BankingMetadata.Account.EntityId, "Account", account => account
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Balance", typeof(decimal))
                .Relationship(
                    new RelationshipId(2),
                    "Transactions",
                    BankingMetadata.Transaction.EntityId,
                    RelationshipCardinality.Many))
            .Entity(BankingMetadata.Transaction.EntityId, "Transaction", transaction => transaction
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Amount", typeof(decimal))
                // FieldId(4) deliberately matches BankingMetadata.Transaction's
                // ColumnId(4) for TransactionDate -- see SqlCandidateSource's
                // remarks on the FieldId/ColumnId alignment this sample relies on.
                .Field(new FieldId(4), "TransactionDate", typeof(DateTime)))
            .Build();
}
