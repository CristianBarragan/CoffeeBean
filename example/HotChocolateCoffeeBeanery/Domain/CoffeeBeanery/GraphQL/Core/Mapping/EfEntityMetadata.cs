using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CoffeeBeanery.GraphQL.Core.Sql;

public sealed class EfEntityMetadata<TContext>
    where TContext : DbContext
{
    private readonly IModel _model;

    public EfEntityMetadata(TContext context)
    {
        _model = context.Model;
    }

    public IEntityType RequireEntityType(
        Type entityType,
        string mappingContext)
    {
        var efType = _model.FindEntityType(entityType);

        if (efType == null)
        {
            throw new InvalidOperationException(
                $"[NodeBuilder] Entity type '{entityType.FullName}' " +
                $"(referenced by mapping '{mappingContext}') was not found in the EF model.");
        }

        return efType;
    }

    public List<NavigationInfo> GetNavigations(IEntityType efEntityType)
    {
        var result = new List<NavigationInfo>();

        foreach (var nav in efEntityType.GetNavigations())
        {
            var fk = nav.ForeignKey;

            // FIX: previously skipped every dependent-side navigation (`if (nav.IsOnDependent)
            // continue;`). That silently dropped any single-reference nav where THIS entity
            // is the one holding the FK column - e.g. CustomerCustomerEdge.InnerCustomer /
            // .OuterCustomer, where CustomerCustomerEdge holds InnerCustomerId/OuterCustomerId
            // pointing at Customer's PK. Both directions are now returned; IsOnDependent is
            // surfaced so callers (NodeBuilder.BuildEntityChildren) can orient FromColumn/
            // ToColumn correctly for whichever side this entity is on - ForeignKeyProperty and
            // PrincipalKeyProperty below are direction-invariant (always "the FK column" /
            // "the PK column" of the underlying relationship), so the caller still needs to
            // know which one lives on THIS entity vs the related one.
            result.Add(new NavigationInfo
            {
                NavigationName = nav.Name,
                ForeignKeyProperty = fk.Properties[0].Name,
                PrincipalKeyProperty = fk.PrincipalKey.Properties[0].Name,
                RelatedEntityType = nav.TargetEntityType.ClrType,
                IsCollection = nav.IsCollection,
                IsOnDependent = nav.IsOnDependent
            });
        }

        return result;
    }

    public sealed class NavigationInfo
    {
        public string NavigationName { get; init; } = "";
        public string ForeignKeyProperty { get; init; } = "";
        public string PrincipalKeyProperty { get; init; } = "";
        public Type RelatedEntityType { get; init; } = typeof(object);
        public bool IsCollection { get; init; }

        // True when the entity this NavigationInfo was generated for is the dependent side
        // of the relationship (i.e. THIS entity holds the FK column, ForeignKeyProperty).
        // False (principal side) means the RELATED entity holds the FK column instead.
        public bool IsOnDependent { get; init; }
    }
}