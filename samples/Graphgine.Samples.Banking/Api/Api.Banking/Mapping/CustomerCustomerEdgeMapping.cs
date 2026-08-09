using Graphgine.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Api.Banking.Mapping;

/// <summary>
/// Maps Domain.Model.CustomerCustomerEdge onto
/// Database.Entity.CustomerCustomerRelationship, and carries the Apache
/// AGE graph edge definition for the same relationship. Ported from
/// legacy Domain.Shared/Mapping/CustomerCustomerEdgeMapping.cs AND
/// CustomerCustomerRelationshipMapping.cs together.
///
/// -----------------------------------------------------------------------
/// Why one file instead of two
/// -----------------------------------------------------------------------
/// The legacy system had a separate CustomerCustomerRelationshipMapping
/// class — but that's a mapping for a model, and there is no
/// Domain.Model.CustomerCustomerRelationship type anywhere in this sample.
/// Every field the legacy relationship model carried
/// (CustomerCustomerRelationshipKey, InnerCustomerKey, OuterCustomerKey,
/// CustomerCustomerRelationshipType) already lives directly on
/// Domain.Model.CustomerCustomerEdge instead — the two models were
/// collapsed into one in this port's domain layer. The legacy GraphMap
/// was oddly declared on CustomerCustomerRelationshipMapping rather than
/// CustomerCustomerEdgeMapping; here it moves to sit with the model that
/// actually owns it.
///
/// -----------------------------------------------------------------------
/// Inner/Outer disambiguation
/// -----------------------------------------------------------------------
/// CustomerCustomerRelationship has two FK edges to the same Customer
/// entity (InnerCustomerId, OuterCustomerId) — plain property-name
/// matching can't tell them apart on its own. The two AliasProperty
/// entries below are what resolve that: EntityNavigationConvention
/// expects a column named "{AliasProperty}Id" on the primary entity for
/// each one (InnerCustomerId / OuterCustomerId), which is exactly what
/// CustomerCustomerRelationshipEntityConfiguration.HasOne(c => c.InnerCustomer)
/// .HasForeignKey(c => c.InnerCustomerId) (and the Outer equivalent)
/// declare. This is also the mechanism that replaces legacy's separate
/// InnerCustomerMapping/OuterCustomerMapping registrations — see
/// CustomerMapping.cs's header comment.
///
/// -----------------------------------------------------------------------
/// FIXED — this was a real schema gap; now resolved
/// -----------------------------------------------------------------------
/// Domain.Model.CustomerCustomerEdge.InnerCustomerKey / OuterCustomerKey
/// are `Guid?`, mirroring the natural key of the linked Customer directly
/// on the edge (same pattern Transaction uses for AccountKey/ContractKey).
/// Database.Entity.CustomerCustomerRelationship previously had no such
/// columns — only the int InnerCustomerId/OuterCustomerId FKs and the
/// InnerCustomer/OuterCustomer navigation properties. It now has
/// InnerCustomerKey/OuterCustomerKey (Guid?) alongside them, so both match
/// by name and type directly against this model's primary entity — no
/// explicit Field entry needed, left to convention below. This addition
/// requires a new EF migration before the sample can run against a real
/// database — see PORT-STATUS.md.
///
/// The Graph block below was written against these columns before the fix
/// landed (see PORT-STATUS.md §3a) and needed no changes once they did —
/// VertexDefinition.ForeignKeyColumn already pointed at
/// InnerCustomerKey/OuterCustomerKey by name.
/// </summary>
public sealed class CustomerCustomerEdgeMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(CustomerCustomerEdge),
        Schema = nameof(DataEntity.Schema.Banking),

        Entities =
        [
            new EntityDefinition
            {
                Entity = typeof(DataEntity.CustomerCustomerRelationship),
                ModelKey = nameof(CustomerCustomerEdge.CustomerCustomerRelationshipKey),
                EntityKey = nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey),
                IsPrimary = true
            },
            new EntityDefinition
            {
                Entity = typeof(DataEntity.Customer),
                ModelKey = nameof(CustomerCustomerEdge.InnerCustomerKey),
                EntityKey = nameof(DataEntity.Customer.CustomerKey),
                AliasProperty = nameof(CustomerCustomerEdge.InnerCustomer)
            },
            new EntityDefinition
            {
                Entity = typeof(DataEntity.Customer),
                ModelKey = nameof(CustomerCustomerEdge.OuterCustomerKey),
                EntityKey = nameof(DataEntity.Customer.CustomerKey),
                AliasProperty = nameof(CustomerCustomerEdge.OuterCustomer)
            }
        ],

        Fields =
        [
            // Same member names both sides (Family/Partner/Widow/Single/Divorced).
            new FieldDefinition
            {
                Source = nameof(CustomerCustomerEdge.CustomerCustomerRelationshipType),
                Entity = typeof(DataEntity.CustomerCustomerRelationship),
                Destination = nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipType),
                EnumMapping = new EnumMappingDefinition<CustomerCustomerRelationshipType, DataEntity.CustomerCustomerRelationshipType>()
            }

            // CustomerCustomerRelationshipKey, InnerCustomerKey and
            // OuterCustomerKey all match by name+type — left to
            // convention, see header comment.
        ],

        // Ported from legacy's GraphMap (previously misplaced on
        // CustomerCustomerRelationshipMapping — see header comment).
        // GraphName matches Database.Graph.Banking's
        // create_graph('CustomerCustomerEdge') call.
        Graph = new GraphDefinition
        {
            GraphName = nameof(CustomerCustomerEdge),
            EdgeLabel = nameof(CustomerCustomerEdge),
            EdgeKey = nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey),
            From = new VertexDefinition
            {
                Label = nameof(Customer),
                GraphProperty = nameof(DataEntity.Customer.CustomerKey),
                ForeignKeyColumn = nameof(CustomerCustomerEdge.InnerCustomerKey),
                Alias = nameof(CustomerCustomerEdge.InnerCustomer)
            },
            To = new VertexDefinition
            {
                Label = nameof(Customer),
                GraphProperty = nameof(DataEntity.Customer.CustomerKey),
                ForeignKeyColumn = nameof(CustomerCustomerEdge.OuterCustomerKey),
                Alias = nameof(CustomerCustomerEdge.OuterCustomer)
            },
            // The model-side column each vertex's identity is read from
            // when writing/reading the edge — matches legacy's
            // FromJoinColumn/ToJoinColumn, both pointed at Customer's own
            // natural key.
            FromJoinColumn = nameof(DataEntity.Customer.CustomerKey),
            ToJoinColumn = nameof(DataEntity.Customer.CustomerKey)
        }
    };
}
