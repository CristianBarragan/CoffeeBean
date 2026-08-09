using Graphgine.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Api.Banking.Mapping;

/// <summary>
/// Maps Domain.Model.Customer onto Database.Entity.Customer.
/// Ported from legacy Domain.Shared/Mapping/CustomerMapping.cs
/// (specifically CustomerBaseMapping&lt;TAlias&gt;.BuildMap — the shared
/// base both InnerCustomerMapping/OuterCustomerMapping built on).
///
/// The legacy InnerCustomer/OuterCustomer alias split — two separate
/// mapping registrations for the same Customer model/entity pair, used
/// only to disambiguate the two parallel FK edges out of
/// CustomerCustomerRelationship — has no equivalent here as a second
/// mapping class. In the new DSL that disambiguation is expressed once,
/// on CustomerCustomerEdgeMapping.cs, via two Entities[] entries with
/// AliasProperty = "InnerCustomer" / "OuterCustomer" pointing at this same
/// Customer entity. EntityNavigationConvention resolves each one against
/// its own FK column using an "{AliasProperty}Id" convention
/// (InnerCustomerId / OuterCustomerId), which is exactly what
/// CustomerCustomerRelationshipEntityConfiguration declares. Customer
/// itself only needs one ordinary mapping.
///
/// Customer.Product and Customer.ContactPoint navigations are left to
/// convention: ContactPoint resolves via the entity FK graph
/// (CustomerEntityConfiguration.HasMany(ContactPoint).HasForeignKey(CustomerId)),
/// and Product — which has no backing entity of its own — is picked up by
/// ModelChildrenInference purely from the Customer.Product property
/// existing on the model (any non-scalar property becomes a ModelChild
/// automatically unless already declared).
/// </summary>
public sealed class CustomerMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Customer),
        Schema = nameof(DataEntity.Schema.Banking),

        Entity = typeof(DataEntity.Customer),
        Key = nameof(DataEntity.Customer.CustomerKey),

        Fields =
        [
            // Names differ between model and entity on all three of these
            // — convention matching (same-name only) would not find them.
            new FieldDefinition
            {
                Source = nameof(Customer.FirstNaming),
                Entity = typeof(DataEntity.Customer),
                Destination = nameof(DataEntity.Customer.FirstName)
            },
            new FieldDefinition
            {
                Source = nameof(Customer.LastNaming),
                Entity = typeof(DataEntity.Customer),
                Destination = nameof(DataEntity.Customer.LastName)
            },
            new FieldDefinition
            {
                Source = nameof(Customer.FullNaming),
                Entity = typeof(DataEntity.Customer),
                Destination = nameof(DataEntity.Customer.FullName)
            },

            // Same member names both sides (Person/Organisation).
            new FieldDefinition
            {
                Source = nameof(Customer.CustomerType),
                Entity = typeof(DataEntity.Customer),
                Destination = nameof(DataEntity.Customer.CustomerType),
                EnumMapping = new EnumMappingDefinition<CustomerType, DataEntity.CustomerType>()
            }

            // CustomerKey matches by name+type — left to convention.
            // ContactPoint/Product navigations — left to convention, see
            // header comment.
        ]
    };
}
