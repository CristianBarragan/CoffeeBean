using Foundgine.Abstractions;
using Foundgine.Semantics;

var model = CoffeeBeaneryProductModel.Build();

Console.WriteLine("CoffeeBeanery Product composite");
Console.WriteLine("--------------------------------");
Console.WriteLine("Product = CustomerBankingRelationship + Contract + Account + Transaction");
Console.WriteLine($"Semantic entities: {model.Entities.Count}");

var product = ProductComposer.Compose(
    new CustomerBankingRelationship(42, Guid.Parse("11111111-1111-1111-1111-111111111111")),
    new Contract(9001, Guid.Parse("22222222-2222-2222-2222-222222222222"), "Mortgage", 425000m),
    new Account(7001, Guid.Parse("33333333-3333-3333-3333-333333333333"), "ACC-0007001", "Home Loan"),
    [
        new Transaction(1, Guid.Parse("44444444-4444-4444-4444-444444444444"), 1250m, 423750m),
        new Transaction(2, Guid.Parse("55555555-5555-5555-5555-555555555555"), 980m, 422770m)
    ]);

Console.WriteLine($"ProductKey: {product.ProductKey}");
Console.WriteLine($"Contract: {product.ContractType} / {product.ContractAmount:N2}");
Console.WriteLine($"Account: {product.AccountNumber} / {product.AccountName}");
Console.WriteLine($"Latest transaction: {product.LatestTransactionAmount:N2}");
Console.WriteLine($"Balance: {product.Balance:N2}");

public static class CoffeeBeaneryProductModel
{
    public static readonly EntityId Product = new(1000);
    public static readonly EntityId CustomerBankingRelationship = new(1001);
    public static readonly EntityId Contract = new(1002);
    public static readonly EntityId Account = new(1003);
    public static readonly EntityId Transaction = new(1004);

    public static SemanticModel Build() =>
        new SemanticModelBuilder()
            .Entity(Product, "Product", e => e
                .Identity(new FieldId(1), "ProductKey")
                .Field(new FieldId(2), "CustomerBankingRelationshipKey", typeof(Guid))
                .Field(new FieldId(3), "ContractType", typeof(string))
                .Field(new FieldId(4), "ContractAmount", typeof(decimal))
                .Field(new FieldId(5), "AccountNumber", typeof(string))
                .Field(new FieldId(6), "AccountName", typeof(string))
                .Field(new FieldId(7), "LatestTransactionAmount", typeof(decimal))
                .Field(new FieldId(8), "Balance", typeof(decimal))
                .Relationship(new RelationshipId(1), "bankingRelationship", CustomerBankingRelationship, RelationshipCardinality.One)
                .Relationship(new RelationshipId(2), "contract", Contract, RelationshipCardinality.One)
                .Relationship(new RelationshipId(3), "account", Account, RelationshipCardinality.One)
                .Relationship(new RelationshipId(4), "transactions", Transaction, RelationshipCardinality.Many))
            .Entity(CustomerBankingRelationship, "CustomerBankingRelationship", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "CustomerBankingRelationshipKey", typeof(Guid)))
            .Entity(Contract, "Contract", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "ContractKey", typeof(Guid))
                .Field(new FieldId(3), "ContractType", typeof(string))
                .Field(new FieldId(4), "Amount", typeof(decimal)))
            .Entity(Account, "Account", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "AccountKey", typeof(Guid))
                .Field(new FieldId(3), "AccountNumber", typeof(string))
                .Field(new FieldId(4), "AccountName", typeof(string)))
            .Entity(Transaction, "Transaction", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "TransactionKey", typeof(Guid))
                .Field(new FieldId(3), "Amount", typeof(decimal))
                .Field(new FieldId(4), "Balance", typeof(decimal)))
            .Build();
}

public static class ProductComposer
{
    public static Product Compose(
        CustomerBankingRelationship relationship,
        Contract contract,
        Account account,
        IReadOnlyList<Transaction> transactions)
    {
        var latest = transactions.LastOrDefault()
            ?? throw new InvalidOperationException("A Product requires at least one Transaction.");

        return new Product(
            contract.ContractKey,
            relationship.CustomerBankingRelationshipKey,
            contract.ContractType,
            contract.Amount,
            account.AccountNumber,
            account.AccountName,
            latest.Amount,
            latest.Balance);
    }
}

public sealed record Product(
    Guid ProductKey, Guid CustomerBankingRelationshipKey, string ContractType,
    decimal ContractAmount, string AccountNumber, string AccountName,
    decimal LatestTransactionAmount, decimal Balance);
public sealed record CustomerBankingRelationship(int Id, Guid CustomerBankingRelationshipKey);
public sealed record Contract(int Id, Guid ContractKey, string ContractType, decimal Amount);
public sealed record Account(int Id, Guid AccountKey, string AccountNumber, string AccountName);
public sealed record Transaction(int Id, Guid TransactionKey, decimal Amount, decimal Balance);
