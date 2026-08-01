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
            },
            // new()
            // {
            //     Entity = typeof(DataEntity.ContactPoint),
            //
            //     ModelKey = nameof(ContactPoint.CustomerKey),
            //
            //     EntityKey = nameof(DataEntity.ContactPoint.CustomerId)
            // },
            // new()
            // {
            //     ModelKey =
            //         nameof(Product.CustomerKey),
            //
            //     AliasProperty =
            //         nameof(Customer.Product)
            // }
        ],
        PrimaryKey = [new()
        {
            Entity = typeof(DataEntity.Customer),
            // ModelKey = nameof(Customer.CustomerKey),
            ColumnKey =
                nameof(DataEntity.Customer.Id)
        }],
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
            },

            new()
            {
                Source =
                    nameof(Customer.ContactPoint),

                Entity =
                    typeof(DataEntity.ContactPoint),

                Destination =
                    nameof(DataEntity.ContactPoint.CustomerKey),

                IsNavigationKey = true
            },

            new()
            {
                Source =
                    nameof(Customer.Product),

                Entity =
                    typeof(DataEntity.CustomerBankingRelationship),

                Destination =
                    nameof(DataEntity.CustomerBankingRelationship.CustomerKey),

                IsNavigationKey = true
            }
        ]
    };
}