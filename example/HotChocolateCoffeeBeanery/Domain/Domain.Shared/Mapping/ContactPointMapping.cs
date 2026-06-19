using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class ContactPointMappingSet : IMappingSet
{
    public void Register()
    {
        new ContactPointMapping().Register();
    }
}

public sealed partial class ContactPointMapping : BaseMappingRegistration<ContactPoint>
{
    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(ContactPoint),
            Schema = nameof(DataEntity.Schema.Banking)
        };

        map.AddModelToEntity<ContactPoint, DataEntity.ContactPoint>(
            m => m.ContactPointKey,
            e => e.ContactPointKey,
            isPrimary: true);

        map.UpsertKeys.Add(
            new UpsertKey(
                nameof(DataEntity.ContactPoint),
                nameof(DataEntity.ContactPoint.ContactPointKey)));

        map.FieldMaps.Add(new FieldMap
        {
            SourceName = nameof(ContactPoint.ContactPointType),
            DestinationEntity = nameof(DataEntity.ContactPoint),
            DestinationName = nameof(DataEntity.ContactPoint.ContactPointType),

            FromEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { ContactPointType.Email.ToString(), (int)ContactPointType.Email },
                { ContactPointType.Landline.ToString(), (int)ContactPointType.Landline },
                { ContactPointType.Mobile.ToString(), (int)ContactPointType.Mobile }
            },

            ToEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { ContactPointType.Email.ToString(), (int)ContactPointType.Email },
                { ContactPointType.Landline.ToString(), (int)ContactPointType.Landline },
                { ContactPointType.Mobile.ToString(), (int)ContactPointType.Mobile }
            }
        });

        return map;
    }
}