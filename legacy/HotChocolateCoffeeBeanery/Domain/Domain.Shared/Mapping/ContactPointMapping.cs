using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class ContactPointMappingSet : IMappingSet<CustomerMappingType, Domain.Model.Model>
{
    public void Register(CustomerMappingType type, Domain.Model.Model model)
    {
        new ContactPointMapping(type.ToString(), model.ToString()).Register();
    }
}

public class ContactPointMapping : BaseMappingRegistration<ContactPoint, DataEntity.ContactPoint>
{
    public ContactPointMapping(string alias, string model) : base(alias, model)
    {
    }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(ContactPoint),
            Schema = nameof(DataEntity.Schema.Banking)
        };

        map.EntityParents.AddRange(new[]
        {
            new LinkKey
            {
                AliasFrom    = A(nameof(DataEntity.ContactPoint)),
                From       = nameof(DataEntity.ContactPoint),
                FromColumn = nameof(DataEntity.ContactPoint.CustomerId),
                AliasTo    = A(nameof(DataEntity.Customer)),
                To         = nameof(DataEntity.Customer),
                ToColumn   = nameof(DataEntity.Customer.Id)
            },
            new LinkKey
            {
                AliasFrom    = A(nameof(DataEntity.ContactPoint)),
                From       = nameof(DataEntity.ContactPoint),
                FromColumn = nameof(DataEntity.ContactPoint.CustomerKey),
                AliasTo    = A(nameof(DataEntity.Customer)),
                To         = nameof(DataEntity.Customer),
                ToColumn   = nameof(DataEntity.Customer.CustomerKey)
            }
        });

        // map.ModelParents.Add(new LinkKey
        // {
        //     AliasFrom    = A(nameof(ContactPoint)),
        //     From       = nameof(ContactPoint),
        //     FromColumn = nameof(ContactPoint.CustomerKey),
        //     AliasTo    = A(nameof(Customer)),
        //     To         = nameof(Customer),
        //     ToColumn   = nameof(Customer.CustomerKey)
        // });

        map.ModelToEntityLinks.Add(new LinkKey
        {
            AliasFrom    = A(nameof(ContactPoint)),
            From       = nameof(ContactPoint),
            FromColumn = nameof(ContactPoint.ContactPointKey),
            AliasTo    = A(nameof(DataEntity.ContactPoint)),
            To         = nameof(DataEntity.ContactPoint),
            ToColumn   = nameof(DataEntity.ContactPoint.ContactPointKey)
        });

        map.UpsertKeys.Add(new UpsertKey(
            nameof(DataEntity.ContactPoint),
            nameof(DataEntity.ContactPoint.ContactPointKey)
        ));

        map.FieldMaps.AddRange(new[]
        {
            new FieldMap { SourceAlias = A(nameof(ContactPoint)), DestinationAlias = A(nameof(DataEntity.ContactPoint)), SourceName = nameof(DataEntity.ContactPoint.Id),      DestinationEntity = nameof(DataEntity.ContactPoint), DestinationName = nameof(DataEntity.ContactPoint.Id) },
            new FieldMap
            {
                SourceAlias = A(nameof(ContactPoint)), DestinationAlias = A(nameof(DataEntity.ContactPoint)), SourceName        = nameof(ContactPoint.ContactPointType),
                DestinationEntity = nameof(DataEntity.ContactPoint),
                DestinationName   = nameof(DataEntity.ContactPoint.ContactPointType),
                FromEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { ContactPointType.Email.ToString(),       (int)ContactPointType.Email },
                    { ContactPointType.Landline.ToString(),       (int)ContactPointType.Landline },
                    { ContactPointType.Mobile.ToString(),       (int)ContactPointType.Mobile }
                },
                ToEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { ContactPointType.Email.ToString(),       (int)ContactPointType.Email },
                    { ContactPointType.Landline.ToString(),       (int)ContactPointType.Landline },
                    { ContactPointType.Mobile.ToString(),       (int)ContactPointType.Mobile }
                }
            },
            new FieldMap { SourceAlias = A(nameof(ContactPoint)), DestinationAlias = A(nameof(DataEntity.ContactPoint)), SourceName = nameof(ContactPoint.ContactPointKey),     DestinationEntity = nameof(DataEntity.ContactPoint), DestinationName = nameof(DataEntity.ContactPoint.ContactPointKey) },
            new FieldMap { SourceAlias = A(nameof(ContactPoint)), DestinationAlias = A(nameof(DataEntity.ContactPoint)), SourceName = nameof(ContactPoint.ContactPointValue),   DestinationEntity = nameof(DataEntity.ContactPoint), DestinationName = nameof(DataEntity.ContactPoint.ContactPointValue) },
            new FieldMap { SourceAlias = A(nameof(ContactPoint)), DestinationAlias = A(nameof(DataEntity.ContactPoint)), SourceName = nameof(ContactPoint.CustomerKey),         DestinationEntity = nameof(DataEntity.ContactPoint), DestinationName = nameof(DataEntity.ContactPoint.CustomerKey) }
        });

        return map;
    }
}