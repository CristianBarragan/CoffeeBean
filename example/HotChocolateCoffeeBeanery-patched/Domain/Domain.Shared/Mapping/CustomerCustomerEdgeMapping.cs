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

        Graph =
            new GraphDefinition
            {
                GraphName = nameof(CustomerCustomerEdge),

                EdgeLabel = nameof(CustomerCustomerEdge),

                EdgeKey = nameof(CustomerCustomerEdge.CustomerCustomerRelationshipKey),

                From = new VertexDefinition
                {
                    Label = nameof(Customer),

                    GraphProperty = nameof(Customer.CustomerKey),

                    ForeignKeyColumn =
                        nameof(DataEntity.Customer.CustomerKey),

                    Alias =
                        nameof(CustomerCustomerEdge.InnerCustomer)
                },

                To = new VertexDefinition
                {
                    Label = nameof(Customer),

                    GraphProperty =
                        nameof(Customer.CustomerKey),

                    ForeignKeyColumn =
                        nameof(DataEntity.Customer.CustomerKey),

                    Alias =
                        nameof(CustomerCustomerEdge.OuterCustomer)
                },

                FromJoinColumn = nameof(Customer.CustomerKey),

                ToJoinColumn = nameof(Customer.CustomerKey)
            },

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
            },

            new()
            {
                Source =
                    nameof(CustomerCustomerEdge.InnerCustomerKey),

                Entity =
                    typeof(DataEntity.Customer),

                Destination =
                    nameof(DataEntity.Customer.CustomerKey),

                IsNavigationKey = true
            },

            new()
            {
                Source =
                    nameof(CustomerCustomerEdge.OuterCustomerKey),

                Entity =
                    typeof(DataEntity.Customer),

                Destination =
                    nameof(DataEntity.Customer.CustomerKey),

                IsNavigationKey = true
            }
        ]
    };
}