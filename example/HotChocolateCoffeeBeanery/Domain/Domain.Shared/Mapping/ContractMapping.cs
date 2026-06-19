using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class ContractMappingSet : IMappingSet
{
    public void Register()
    {
        new ContractMapping().Register();
    }
}

public sealed partial class ContractMapping : BaseMappingRegistration<Contract>
{
    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(Contract),
            Schema = nameof(DataEntity.Schema.Lending)
        };

        map.AddModelToEntity<Contract, DataEntity.Contract>(
            m => m.ContractKey,
            e => e.ContractKey,
            isPrimary: true);

        map.UpsertKeys.Add(
            new UpsertKey(
                nameof(DataEntity.Contract),
                nameof(DataEntity.Contract.ContractKey)));

        return map;
    }
}