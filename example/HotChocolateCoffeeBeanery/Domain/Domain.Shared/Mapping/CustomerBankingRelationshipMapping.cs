using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class CustomerBankingRelationshipMappingSet
    : IMappingSet
{
    public void Register()
    {
        new CustomerBankingRelationshipMapping().Register();
    }
}

public sealed partial class CustomerBankingRelationshipMapping
    : BaseMappingRegistration<CustomerBankingRelationship>
{
    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(CustomerBankingRelationship),
            Schema = nameof(DataEntity.Schema.Banking)
        };

        map.AddModelToEntity<CustomerBankingRelationship,
            DataEntity.CustomerBankingRelationship>(
            m => m.CustomerBankingRelationshipKey,
            e => e.CustomerBankingRelationshipKey,
            isPrimary: true);

        map.UpsertKeys.Add(
            new UpsertKey(
                nameof(DataEntity.CustomerBankingRelationship),
                nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)));

        return map;
    }
}