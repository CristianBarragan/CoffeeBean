using Foundgine.Metadata;

namespace Foundgine.Semantics;

public sealed class SemanticModelBuilder
{
    private readonly SemanticModel _model = new();

    public SemanticModelBuilder Entity(
        EntityId id,
        string name,
        Action<SemanticEntityBuilder> configure)
    {
        var builder = new SemanticEntityBuilder(id, name);
        configure(builder);
        _model.Register(builder.Build());
        return this;
    }

    public SemanticModel Build() => _model;
}
