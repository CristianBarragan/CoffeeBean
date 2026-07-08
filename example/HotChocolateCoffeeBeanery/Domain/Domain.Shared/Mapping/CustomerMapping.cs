using CoffeeBeanery.GraphQL.Core.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public partial class CustomerMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Customer),

        Schema = nameof(DataEntity.Schema.Banking),

        Entities =
        [
            new()
            {
                Entity = typeof(DataEntity.Customer),

                ModelKey =
                    nameof(Customer.CustomerKey),

                EntityKey =
                    nameof(DataEntity.Customer.CustomerKey),

                IsPrimary = true
            }
        ],

        UpsertKeys =
        [
            new()
            {
                Entity = typeof(DataEntity.Customer),

                Column =
                    nameof(DataEntity.Customer.CustomerKey)
            }
        ],

        Fields =
        [
            new()
            {
                Source =
                    nameof(Customer.CustomerType),

                Entity =
                    typeof(DataEntity.Customer),

                Destination =
                    nameof(DataEntity.Customer.CustomerType),

                EnumMapping =
                    new EnumMappingDefinition<
                        CustomerType,
                        DataEntity.CustomerType>()
            },

            new()
            {
                Source =
                    nameof(Customer.FirstNaming),

                Entity =
                    typeof(DataEntity.Customer),

                Destination =
                    nameof(DataEntity.Customer.FirstName)
            },

            new()
            {
                Source =
                    nameof(Customer.LastNaming),

                Entity =
                    typeof(DataEntity.Customer),

                Destination =
                    nameof(DataEntity.Customer.LastName)
            },

            new()
            {
                Source =
                    nameof(Customer.FullNaming),

                Entity =
                    typeof(DataEntity.Customer),

                Destination =
                    nameof(DataEntity.Customer.FullName)
            }
        ]
    };
}