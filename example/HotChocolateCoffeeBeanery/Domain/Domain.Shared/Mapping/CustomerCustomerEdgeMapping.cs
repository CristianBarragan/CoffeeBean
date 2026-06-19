using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public sealed class CustomerCustomerEdgeMappingSet : IMappingSet
{
    public void Register()
    {
        new CustomerCustomerEdgeMapping().Register();
    }
}

public sealed partial class CustomerCustomerEdgeMapping
    : BaseMappingRegistration<CustomerCustomerEdge>
{
    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(CustomerCustomerEdge),
            Schema = nameof(DataEntity.Schema.Banking),
            IsGraph = true
        };

        map.AddModelToEntity<CustomerCustomerEdge, DataEntity.CustomerCustomerRelationship>(
            m => m.CustomerCustomerRelationshipKey,
            e => e.CustomerCustomerRelationshipKey,
            isPrimary: true);

        map.AddModelToEntity<CustomerCustomerEdge, DataEntity.Customer>(
            m => m.InnerCustomerKey,
            e => e.CustomerKey,
            alias: m => m.InnerCustomer);

        map.AddModelToEntity<CustomerCustomerEdge, DataEntity.Customer>(
            m => m.OuterCustomerKey,
            e => e.CustomerKey,
            alias: m => m.OuterCustomer);

        map.UpsertKeys.Add(
            new UpsertKey(
                nameof(DataEntity.CustomerCustomerRelationship),
                nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey)));

        map.FieldMaps.Add(new FieldMap
        {
            SourceName = nameof(CustomerCustomerEdge.CustomerCustomerRelationshipType),
            DestinationEntity = nameof(DataEntity.CustomerCustomerRelationship),
            DestinationName = nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipType),

            FromEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { CustomerCustomerRelationshipType.Family.ToString(), (int)CustomerCustomerRelationshipType.Family },
                { CustomerCustomerRelationshipType.Partner.ToString(), (int)CustomerCustomerRelationshipType.Partner },
                { CustomerCustomerRelationshipType.Widow.ToString(), (int)CustomerCustomerRelationshipType.Widow },
                { CustomerCustomerRelationshipType.Single.ToString(), (int)CustomerCustomerRelationshipType.Single },
                { CustomerCustomerRelationshipType.Divorced.ToString(), (int)CustomerCustomerRelationshipType.Divorced }
            },

            ToEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    DataEntity.CustomerCustomerRelationshipType.Family.ToString(),
                    (int)DataEntity.CustomerCustomerRelationshipType.Family
                },
                {
                    DataEntity.CustomerCustomerRelationshipType.Partner.ToString(),
                    (int)DataEntity.CustomerCustomerRelationshipType.Partner
                },
                {
                    DataEntity.CustomerCustomerRelationshipType.Widow.ToString(),
                    (int)DataEntity.CustomerCustomerRelationshipType.Widow
                },
                {
                    DataEntity.CustomerCustomerRelationshipType.Single.ToString(),
                    (int)DataEntity.CustomerCustomerRelationshipType.Single
                },
                {
                    DataEntity.CustomerCustomerRelationshipType.Divorced.ToString(),
                    (int)DataEntity.CustomerCustomerRelationshipType.Divorced
                }
            }
        });
        
        map.GraphMap = new GraphMap
        {
            GraphName     = G(nameof(CustomerCustomerEdge)),
            EdgeLabel     = nameof(CustomerCustomerEdge),
            EdgeKeyColumn = nameof(Domain.Model.CustomerCustomerEdge.CustomerCustomerRelationshipKey),
            FromVertex = new GraphVertex { Label = nameof(Customer), KeyColumn = nameof(Domain.Model.CustomerCustomerEdge.InnerCustomerKey), 
                AliasTo = $"{nameof(DataEntity.CustomerCustomerRelationship.InnerCustomer)}{nameof(DataEntity.Customer)}"},
            ToVertex   = new GraphVertex { Label = nameof(Customer), KeyColumn = nameof(Domain.Model.CustomerCustomerEdge.OuterCustomerKey), 
                AliasTo = $"{nameof(DataEntity.CustomerCustomerRelationship.OuterCustomer)}{nameof(DataEntity.Customer)}"},
            FromJoinColumn = nameof(Customer.CustomerKey),
            ToJoinColumn   = nameof(Customer.CustomerKey)
        };

        return map;
    }
}