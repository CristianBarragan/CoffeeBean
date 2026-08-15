using Foundgine.Abstractions;
using Foundgine.Semantics;
using Foundgine.Semantics.Resolution;

const int Contract = 1;
const int Account = 2;
const int CustomerBankingRelationship = 3;
const int Transaction = 4;

var model = new SemanticModelBuilder()
    // CoffeeBeanery domain entities.
    .Entity(new EntityId(Contract), "Contract", e => e
        .Identity(new FieldId(1), "Id")
        .Field(new FieldId(2), "ContractKey", typeof(Guid))
        .Field(new FieldId(3), "ContractType", typeof(string))
        .Field(new FieldId(4), "Amount", typeof(decimal))
        .Relationship(new RelationshipId(101), "Account", new EntityId(Account), RelationshipCardinality.One)
        .Relationship(new RelationshipId(102), "CustomerBankingRelationship", new EntityId(CustomerBankingRelationship), RelationshipCardinality.One)
        .Relationship(new RelationshipId(103), "Transactions", new EntityId(Transaction), RelationshipCardinality.Many))
    .Entity(new EntityId(Account), "Account", e => e
        .Identity(new FieldId(1), "Id")
        .Field(new FieldId(2), "AccountKey", typeof(Guid))
        .Field(new FieldId(3), "AccountNumber", typeof(string))
        .Field(new FieldId(4), "AccountName", typeof(string)))
    .Entity(new EntityId(CustomerBankingRelationship), "CustomerBankingRelationship", e => e
        .Identity(new FieldId(1), "Id")
        .Field(new FieldId(2), "CustomerId", typeof(int))
        .Field(new FieldId(3), "CustomerKey", typeof(Guid)))
    .Entity(new EntityId(Transaction), "Transaction", e => e
        .Identity(new FieldId(1), "Id")
        .Field(new FieldId(2), "TransactionKey", typeof(Guid))
        .Field(new FieldId(3), "Amount", typeof(decimal))
        .Field(new FieldId(4), "Balance", typeof(decimal))
        .Field(new FieldId(5), "ContractId", typeof(int))
        .Field(new FieldId(6), "AccountId", typeof(int)))
    .Build();

// Product is a semantic composite, not a new storage entity.
// One request composes four CoffeeBeanery entities into one product view.
var product = new SemanticRequest(
    new EntityId(Contract),
    [
        new SemanticSelection(new FieldId(1), null, []),
        new SemanticSelection(new FieldId(2), null, []),
        new SemanticSelection(new FieldId(3), null, []),
        new SemanticSelection(new FieldId(4), null, []),
        new SemanticSelection(null, new RelationshipId(101),
        [
            new SemanticSelection(new FieldId(1), null, []),
            new SemanticSelection(new FieldId(3), null, []),
            new SemanticSelection(new FieldId(4), null, [])
        ]),
        new SemanticSelection(null, new RelationshipId(102),
        [
            new SemanticSelection(new FieldId(1), null, []),
            new SemanticSelection(new FieldId(2), null, []),
            new SemanticSelection(new FieldId(3), null, [])
        ]),
        new SemanticSelection(null, new RelationshipId(103),
        [
            new SemanticSelection(new FieldId(1), null, []),
            new SemanticSelection(new FieldId(3), null, []),
            new SemanticSelection(new FieldId(4), null, []),
            new SemanticSelection(new FieldId(5), null, []),
            new SemanticSelection(new FieldId(6), null, [])
        ])
    ]);

var resolved = new SemanticRequestResolver(model).Resolve(product);
var root = resolved.Nodes.Single(node => node.ParentId is null);

Console.WriteLine("CoffeeBeanery Product composite");
Console.WriteLine($"Root: {model.Get(root.EntityId).Name}");
Console.WriteLine("Composition: Contract + Account + CustomerBankingRelationship + Transaction");
Console.WriteLine($"Graph nodes: {resolved.Nodes.Count}");
