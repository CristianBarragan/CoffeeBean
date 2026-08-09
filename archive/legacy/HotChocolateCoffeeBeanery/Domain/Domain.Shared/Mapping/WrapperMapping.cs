using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class WrapperMappingSet : IMappingSet<CustomerMappingType, Domain.Model.Model>
{
    public void Register(CustomerMappingType type, Domain.Model.Model model)
    {
        new WrapperMapping(type.ToString(), model.ToString()).Register();
    }
}

public class WrapperMapping
    : BaseModelMappingRegistration<Wrapper>
{
    public WrapperMapping(string alias, string model) : base(alias, model)
    {
    }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap()
        {
            ModelName = nameof(Wrapper)
        };

        map.ModelChildren.AddRange(new[]
        {
            new LinkKey { 
                AliasFrom    = A(nameof(Wrapper)), 
                From = nameof(Wrapper), 
                AliasTo = A(nameof(CustomerCustomerEdge)), 
                To = nameof(CustomerCustomerEdge)
            }
        });

        return map;
    }
}