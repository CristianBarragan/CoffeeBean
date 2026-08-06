using CoffeeBeanery.GraphQL.Core.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public partial class ContractMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Contract),

        Schema = nameof(DataEntity.Schema.Lending),

        Entity = typeof(DataEntity.Contract),

        Key = nameof(Contract.ContractKey)
    };
}