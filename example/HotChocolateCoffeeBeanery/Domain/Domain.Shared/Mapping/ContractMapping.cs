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

        Entities =
        [
            new()
            {
                Entity = typeof(DataEntity.Contract),

                ModelKey =
                    nameof(Contract.ContractKey),

                EntityKey =
                    nameof(DataEntity.Contract.ContractKey),

                IsPrimary = true
            }
        ],
        // PrimaryKey = [new()
        // {
        //     Entity = typeof(DataEntity.Contract),
        //     // ModelKey = nameof(DataEntity.Contract.Id),
        //     ColumnKey =
        //         nameof(DataEntity.Contract.Id)
        // }],
        UpsertKeys =
        [
            new()
            {
                Entity = typeof(DataEntity.Contract),

                Column =
                    nameof(DataEntity.Contract.ContractKey)
            }
        ]
    };
}