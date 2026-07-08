using CoffeeBeanery.GraphQL.Core.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public partial class CustomerCustomerEdgeMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(CustomerCustomerEdge),

        Schema = nameof(DataEntity.Schema.Banking),

        IsGraph = true,

        Entities =
        [
            new()
            {
                Entity = typeof(DataEntity.CustomerCustomerRelationship),

                ModelKey =
                    nameof(CustomerCustomerEdge.CustomerCustomerRelationshipKey),

                EntityKey =
                    nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey),

                IsPrimary = true
            },

            new()
            {
                Entity = typeof(DataEntity.Customer),

                ModelKey =
                    nameof(CustomerCustomerEdge.InnerCustomerKey),

                EntityKey =
                    nameof(DataEntity.Customer.CustomerKey),

                AliasProperty =
                    nameof(CustomerCustomerEdge.InnerCustomer)
            },

            new()
            {
                Entity = typeof(DataEntity.Customer),

                ModelKey =
                    nameof(CustomerCustomerEdge.OuterCustomerKey),

                EntityKey =
                    nameof(DataEntity.Customer.CustomerKey),

                AliasProperty =
                    nameof(CustomerCustomerEdge.OuterCustomer)
            }
        ],

        UpsertKeys =
        [
            new()
            {
                Entity =
                    typeof(DataEntity.CustomerCustomerRelationship),

                Column =
                    nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey)
            }
        ],

        Fields =
        [
            new()
            {
                Source =
                    nameof(CustomerCustomerEdge.CustomerCustomerRelationshipType),

                Entity =
                    typeof(DataEntity.CustomerCustomerRelationship),

                Destination =
                    nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipType),

                EnumMapping =
                    new EnumMappingDefinition<
                        CustomerCustomerRelationshipType,
                        DataEntity.CustomerCustomerRelationshipType>()
            }
        ],

        Graph =
            new GraphDefinition
            {
                GraphName =
                    nameof(CustomerCustomerEdge),

                EdgeLabel =
                    nameof(CustomerCustomerEdge),

                EdgeKey =
                    nameof(CustomerCustomerEdge.CustomerCustomerRelationshipKey),

                From =
                    new VertexDefinition
                    {
                        Label =
                            nameof(Customer),

                        KeyColumn =
                            nameof(CustomerCustomerEdge.InnerCustomerKey),

                        Alias =
                            $"{nameof(DataEntity.CustomerCustomerRelationship.InnerCustomer)}{nameof(DataEntity.Customer)}"
                    },

                To =
                    new VertexDefinition
                    {
                        Label =
                            nameof(Customer),

                        KeyColumn =
                            nameof(CustomerCustomerEdge.OuterCustomerKey),

                        Alias =
                            $"{nameof(DataEntity.CustomerCustomerRelationship.OuterCustomer)}{nameof(DataEntity.Customer)}"
                    },

                FromJoinColumn =
                    nameof(Customer.CustomerKey),

                ToJoinColumn =
                    nameof(Customer.CustomerKey)
            }
    };
}