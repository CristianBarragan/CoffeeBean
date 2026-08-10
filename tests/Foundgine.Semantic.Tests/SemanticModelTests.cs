using Foundgine.Metadata;
using Xunit;

namespace Foundgine.Semantic.Tests;

/// <summary>
/// Pins the Milestone 1 acceptance test from docs/00-Direction/Milestones.md
/// almost verbatim: given the Banking domain, Foundgine can enumerate
/// Customer / Account / Transaction with their identity, fields,
/// relationships, and (empty) actions.
/// </summary>
public class BankingAcceptanceTests
{
    private static readonly EntityId CustomerId = new(1);
    private static readonly EntityId AccountId = new(2);
    private static readonly EntityId TransactionId = new(3);

    private static SemanticModel BuildBankingModel() =>
        new SemanticModelBuilder()
            .Entity(CustomerId, "Customer", customer => customer
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Name", typeof(string))
                .Relationship(new RelationshipId(1), "Accounts", AccountId, RelationshipCardinality.Many))
            .Entity(AccountId, "Account", account => account
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Balance", typeof(decimal))
                .Relationship(new RelationshipId(2), "Transactions", TransactionId, RelationshipCardinality.Many))
            .Entity(TransactionId, "Transaction", transaction => transaction
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Amount", typeof(decimal)))
            .Build();

    [Fact]
    public void Customer_HasIdIdentity_NameField_AccountsRelationship_AndNoActions()
    {
        var model = BuildBankingModel();

        var customer = model.Get(CustomerId);

        Assert.Equal("Id", customer.Identity.Name);
        Assert.Equal(new[] { "Name" }, customer.Fields.Select(f => f.Name));
        Assert.Equal(new[] { "Accounts" }, customer.Relationships.Select(r => r.Name));
        Assert.Empty(customer.Actions);
    }

    [Fact]
    public void Account_HasIdIdentity_BalanceField_AndTransactionsRelationship()
    {
        var model = BuildBankingModel();

        var account = model.Get(AccountId);

        Assert.Equal("Id", account.Identity.Name);
        Assert.Equal(new[] { "Balance" }, account.Fields.Select(f => f.Name));
        Assert.Equal(new[] { "Transactions" }, account.Relationships.Select(r => r.Name));
    }

    [Fact]
    public void Transaction_HasIdIdentity_AndAmountField_WithNoRelationships()
    {
        var model = BuildBankingModel();

        var transaction = model.Get(TransactionId);

        Assert.Equal("Id", transaction.Identity.Name);
        Assert.Equal(new[] { "Amount" }, transaction.Fields.Select(f => f.Name));
        Assert.Empty(transaction.Relationships);
    }

    [Fact]
    public void Model_Enumerates_AllThreeEntities()
    {
        var model = BuildBankingModel();

        Assert.Equal(3, model.Entities.Count);
        Assert.Contains(model.Entities, e => e.Name == "Customer");
        Assert.Contains(model.Entities, e => e.Name == "Account");
        Assert.Contains(model.Entities, e => e.Name == "Transaction");
    }

    [Fact]
    public void Printer_RendersCustomer_AsIdentityFieldsRelationshipTree()
    {
        var model = BuildBankingModel();

        var text = SemanticModelPrinter.Describe(model.Get(CustomerId));

        Assert.Equal(
            "Customer" + Environment.NewLine +
            " ├── identity: Id" + Environment.NewLine +
            " ├── fields: Name" + Environment.NewLine +
            " └── relationship: Accounts",
            text);
    }

    [Fact]
    public void Printer_RendersTransaction_WithNoRelationshipRow()
    {
        var model = BuildBankingModel();

        var text = SemanticModelPrinter.Describe(model.Get(TransactionId));

        Assert.Equal(
            "Transaction" + Environment.NewLine +
            " ├── identity: Id" + Environment.NewLine +
            " └── fields: Amount",
            text);
    }
}

public class SemanticModelTests
{
    [Fact]
    public void TryGet_ReturnsFalse_ForUnregisteredEntity()
    {
        var model = new SemanticModelBuilder().Build();

        var found = model.TryGet(new EntityId(99), out var entity);

        Assert.False(found);
        Assert.Null(entity);
    }

    [Fact]
    public void Get_Throws_ForUnregisteredEntity()
    {
        var model = new SemanticModelBuilder().Build();

        var ex = Assert.Throws<KeyNotFoundException>(() => model.Get(new EntityId(99)));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void Entity_WithoutIdentity_ThrowsOnBuild_NeverInventsOne()
    {
        var builder = new SemanticModelBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.Entity(new EntityId(1), "Broken", _ => { }));

        Assert.Contains("Broken", ex.Message);
        Assert.Contains("identity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

public class SearchCapabilityTests
{
    [Fact]
    public void Entity_CanDeclareSearchCapability_ForFreeTextResolution()
    {
        var model = new SemanticModelBuilder()
            .Entity(new EntityId(1), "Customer", customer => customer
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Name", typeof(string))
                .Search(new SearchCapability([new FieldId(2)], SearchStrategy.Fuzzy)))
            .Build();

        var customer = model.Get(new EntityId(1));

        Assert.NotNull(customer.Search);
        Assert.Equal(SearchStrategy.Fuzzy, customer.Search!.Strategy);
        Assert.Equal(new[] { new FieldId(2) }, customer.Search.SearchableFields);
    }

    [Fact]
    public void Entity_WithoutSearchCapability_HasNullSearch()
    {
        var model = new SemanticModelBuilder()
            .Entity(new EntityId(1), "Transaction", t => t.Identity(new FieldId(1), "Id"))
            .Build();

        Assert.Null(model.Get(new EntityId(1)).Search);
    }
}

public class ActionAndPolicyDescriptorTests
{
    [Fact]
    public void NonMutating_Factory_ProducesEmptyRequirementLists()
    {
        var action = ActionDescriptor.NonMutating(
            "GetBalance",
            new EntityId(2),
            new ActionParameter("accountId", typeof(int)));

        Assert.False(action.IsMutating);
        Assert.Empty(action.AuthorizationRequirements);
        Assert.Empty(action.SideEffects);
        Assert.Empty(action.VerificationRequirements);
        Assert.Single(action.Inputs);
    }

    [Fact]
    public void Entity_CanExposeAMutatingAction_WithAuthorizationRequirements()
    {
        var issueRefund = new ActionDescriptor(
            "IssueRefund",
            Target: new EntityId(2),
            Inputs: [new ActionParameter("amount", typeof(decimal))],
            IsMutating: true,
            AuthorizationRequirements: ["Refund permission", "Customer ownership"],
            SideEffects: ["Decreases account balance"],
            VerificationRequirements: ["Re-read account balance after execution"]);

        var model = new SemanticModelBuilder()
            .Entity(new EntityId(2), "Account", account => account
                .Identity(new FieldId(1), "Id")
                .Action(issueRefund))
            .Build();

        var account = model.Get(new EntityId(2));

        Assert.Single(account.Actions);
        Assert.True(account.Actions[0].IsMutating);
        Assert.Equal(2, account.Actions[0].AuthorizationRequirements.Count);
    }

    [Fact]
    public void Entity_WithoutDeclaredPolicies_HasEmptyEffectivePolicies()
    {
        var model = new SemanticModelBuilder()
            .Entity(new EntityId(1), "Account", a => a.Identity(new FieldId(1), "Id"))
            .Build();

        Assert.Empty(model.Get(new EntityId(1)).EffectivePolicies);
    }

    [Fact]
    public void Entity_CanDeclareAPolicy_ForAFutureAction()
    {
        var model = new SemanticModelBuilder()
            .Entity(new EntityId(1), "Account", a => a
                .Identity(new FieldId(1), "Id")
                .Policy(new PolicyDescriptor("RefundLimit", "amount <= configured limit")))
            .Build();

        var policy = Assert.Single(model.Get(new EntityId(1)).EffectivePolicies);
        Assert.Equal("RefundLimit", policy.Name);
    }
}
