using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;

namespace Domain.Shared.Mapping;

public class WrapperMappingSet : IMappingSet
{
    public void Register()
    {
        new WrapperMapping().Register();
    }
}

public sealed partial class WrapperMapping : BaseMappingRegistration<Wrapper>
{
    protected override NodeMap BuildMap()
    {
        return new NodeMap
        {
            ModelName = nameof(Wrapper),
            IsRoot = true
        };
    }
}