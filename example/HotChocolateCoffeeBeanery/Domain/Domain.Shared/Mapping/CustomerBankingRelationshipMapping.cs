using CoffeeBeanery.GraphQL.Core.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public partial class CustomerBankingRelationshipMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(CustomerBankingRelationship),

        Schema = nameof(DataEntity.Schema.Banking),

        Entities =
        [
            new()
            {
                Entity = typeof(DataEntity.CustomerBankingRelationship),

                ModelKey =
                    nameof(CustomerBankingRelationship.CustomerBankingRelationshipKey),

                EntityKey =
                    nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey),

                IsPrimary = true
            }
        ],

        UpsertKeys =
        [
            new()
            {
                Entity = typeof(DataEntity.CustomerBankingRelationship),

                Column =
                    nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
            }
        ]
    };
}