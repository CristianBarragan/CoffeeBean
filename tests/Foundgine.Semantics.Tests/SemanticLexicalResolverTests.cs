using Foundgine.Abstractions;
using Foundgine.Semantics.Resolution;
using Xunit;

namespace Foundgine.Semantics.Tests;

public sealed class SemanticLexicalResolverTests
{
    [Fact]
    public void Resolver_generates_all_kinds_and_uses_highest_root_before_graph_constrained_walk()
    {
        var customer = new EntityId(1);
        var order = new EntityId(2);
        var line = new EntityId(3);
        var product = new EntityId(4);
        var category = new EntityId(5);

        var model = new SemanticModelBuilder()
            .Entity(customer, "Customer", e => e.Identity(new FieldId(101), "Id"))
            .Entity(order, "SalesOrder", e => e.Identity(new FieldId(201), "Id"))
            .Entity(line, "SalesOrderLine", e => e.Identity(new FieldId(301), "Id"))
            .Entity(product, "CatalogProduct", e => e
                .Identity(new FieldId(401), "Id")
                .Field(new FieldId(402), "Name", typeof(string)))
            .Entity(category, "Category", e => e
                .Identity(new FieldId(501), "Id")
                .Field(new FieldId(502), "Name", typeof(string)))
            .Relationship<Dummy, Dummy>(customer, new RelationshipId(1), "Orders", x => x.Id, order, x => x.Id, RelationshipCardinality.Many)
            .Relationship<Dummy, Dummy>(order, new RelationshipId(2), "Lines", x => x.Id, line, x => x.Id, RelationshipCardinality.Many)
            .Relationship<Dummy, Dummy>(line, new RelationshipId(3), "Product", x => x.Id, product, x => x.Id, RelationshipCardinality.One)
            .Relationship<Dummy, Dummy>(product, new RelationshipId(4), "Category", x => x.Id, category, x => x.Id, RelationshipCardinality.One)
            .Build()
            .Freeze()
            .CreateSnapshot();

        var source = new FakeLexicalSource(
            new SemanticLexicalCandidate("bought", SemanticLexicalCandidateKind.Relationship, "Orders", .98,
                RelationshipId: new RelationshipId(1), SourceEntityId: customer, TargetEntityId: order),
            new SemanticLexicalCandidate("bought", SemanticLexicalCandidateKind.Operation, "Buy", .995),
            new SemanticLexicalCandidate("nike", SemanticLexicalCandidateKind.Value, "Nike", .99,
                EntityId: product, FieldId: new FieldId(402), Value: "Nike"),
            new SemanticLexicalCandidate("shoes", SemanticLexicalCandidateKind.Value, "Shoes", .97,
                EntityId: category, FieldId: new FieldId(502), Value: "Shoes"));

        var result = new SemanticLexicalResolver(model, source).Resolve("bought nike shoes");

        Assert.Equal(SemanticLexicalResolutionOutcome.Resolved, result.Outcome);
        Assert.Equal(customer, result.RootEntity);
        Assert.Equal(3, result.Steps.Count);
        Assert.Equal("Orders", result.Steps[0].Candidate.CanonicalName);
        Assert.Equal("Nike", result.Steps[1].Candidate.CanonicalName);
        Assert.Equal("Shoes", result.Steps[2].Candidate.CanonicalName);
        Assert.Equal(2, result.Steps[1].BridgingPath.Count);
        Assert.Single(result.Steps[2].BridgingPath);
        Assert.Contains(source.Requests, x => x.Token == "bought" && x.EffectiveKinds.Count == Enum.GetValues<SemanticLexicalCandidateKind>().Length);
    }

    [Fact]
    public void Resolver_backtracks_when_highest_lexical_root_cannot_form_a_complete_path()
    {
        var customer = new EntityId(1);
        var product = new EntityId(2);

        var model = new SemanticModelBuilder()
            .Entity(customer, "Customer", e => e.Identity(new FieldId(101), "Id"))
            .Entity(product, "Product", e => e.Identity(new FieldId(201), "Id"))
            .Relationship<Dummy, Dummy>(customer, new RelationshipId(1), "Orders", x => x.Id, product, x => x.Id, RelationshipCardinality.Many)
            .Build()
            .Freeze()
            .CreateSnapshot();

        var source = new FakeLexicalSource(
            new SemanticLexicalCandidate("acquired", SemanticLexicalCandidateKind.Relationship, "Wrong", .99,
                RelationshipId: new RelationshipId(99), SourceEntityId: new EntityId(99), TargetEntityId: product),
            new SemanticLexicalCandidate("acquired", SemanticLexicalCandidateKind.Relationship, "Orders", .85,
                RelationshipId: new RelationshipId(1), SourceEntityId: customer, TargetEntityId: product),
            new SemanticLexicalCandidate("shoes", SemanticLexicalCandidateKind.Value, "Shoes", .95,
                EntityId: product, FieldId: new FieldId(201), Value: "Shoes"));

        var result = new SemanticLexicalResolver(model, source).Resolve("acquired shoes");

        Assert.Equal(SemanticLexicalResolutionOutcome.Resolved, result.Outcome);
        Assert.Equal("Orders", result.Steps[0].Candidate.CanonicalName);
        Assert.Equal(customer, result.RootEntity);
    }

    private sealed class Dummy
    {
        public int Id { get; init; }
    }

    private sealed class FakeLexicalSource(params SemanticLexicalCandidate[] candidates) : ISemanticLexicalCandidateSource
    {
        public List<SemanticLexicalRequest> Requests { get; } = [];

        public IReadOnlyList<SemanticLexicalCandidate> Retrieve(SemanticLexicalRequest request)
        {
            Requests.Add(request);
            return candidates
                .Where(x => string.Equals(x.Token, request.Token, StringComparison.OrdinalIgnoreCase))
                .Where(x => request.EffectiveKinds.Contains(x.Kind))
                .OrderByDescending(x => x.Score)
                .ToArray();
        }
    }
}
