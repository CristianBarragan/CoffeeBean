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

        Entity = typeof(DataEntity.CustomerBankingRelationship),

        Key = nameof(CustomerBankingRelationship.CustomerBankingRelationshipKey),
        
        Fields =
        [
            new()
            {
                Source =
                    nameof(CustomerBankingRelationship.Contract),

                Entity =
                    typeof(DataEntity.Contract),

                Destination =
                    nameof(DataEntity.Contract.CustomerBankingRelationshipId),

                IsNavigationKey = true
            }
        ]
    };
}