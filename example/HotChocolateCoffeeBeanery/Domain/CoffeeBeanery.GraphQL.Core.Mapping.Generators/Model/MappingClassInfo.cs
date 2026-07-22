using System;
using System.Collections.Generic;
using System.Linq;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes;
using Microsoft.CodeAnalysis;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model
{
    
    public sealed class MappingClassInfo
    {
        public INamedTypeSymbol ClassSymbol { get; set; } = null!;

        // Graph/model type
        public INamedTypeSymbol ModelType { get; set; } = null!;
        
        public bool IsComposite { get; set; }

        // Primary Entity type — derived from Definition.Entities where IsPrimary = true.
        // Set explicitly by MappingClassParser after ParseEntities runs.
        public INamedTypeSymbol? EntityType { get; set; }
        
        public IReadOnlyList<EntityDefinitionInfo> ModelToEntity
            => Definition.Entities;

        public string ClassName =>
            ClassSymbol.Name;

        public string Namespace =>
            ClassSymbol.ContainingNamespace?.IsGlobalNamespace == false
                ? ClassSymbol.ContainingNamespace.ToDisplayString()
                : "";

        public string Alias { get; set; } = "";

        public string Prefix { get; set; } = "";

        public string Schema { get; set; } = "";

        public bool IsModel { get; set; }

        public bool IsEntity { get; set; }

        public bool IsGraph { get; set; }

        public GraphInfo? Graph { get; set; }
        
        public MappingDefinitionInfo Definition { get; set; } = new MappingDefinitionInfo();

        public List<FieldInfo> FieldMaps { get; } = new();

        public List<FieldInfo> ManualFieldMaps { get; } = new();

        public List<ExcludedFieldMappingInfo> ExcludedFieldMappings { get; } = new();

        public List<ModelChildInfo> ModelChildren { get; } = new();

        public List<UpsertKeyInfo> UpsertKeys { get; } = new();

        public List<Diagnostic> Diagnostics { get; } = new();

        public List<AutoChildAttachmentInfo> AutoChildAttachments { get; } = new();

        public List<CteUpdateMetaInfo> CteUpdateMeta { get; } = new();

        public string Id { get; set; } = "";
        
        /// <summary>
        /// Creates an independent copy for pipelines (like the global emitter)
        /// that must not mutate the shared instance produced by the per-class
        /// pipeline. All mutable collections are deep-copied; immutable/value
        /// properties are copied by reference/value as-is.
        /// </summary>
        public MappingClassInfo Clone()
        {
            var copy = new MappingClassInfo
            {
                ClassSymbol = ClassSymbol,
                ModelType = ModelType,
                IsComposite = IsComposite,
                EntityType = EntityType,
                Alias = Alias,
                Prefix = Prefix,
                Schema = Schema,
                IsModel = IsModel,
                IsEntity = IsEntity,
                IsGraph = IsGraph,
                Graph = Graph,
                Definition = Definition, // MappingDefinitionInfo is a record; treated as immutable snapshot
                Id = Id
            };

            copy.FieldMaps.AddRange(FieldMaps);
            copy.ManualFieldMaps.AddRange(ManualFieldMaps);
            copy.ExcludedFieldMappings.AddRange(ExcludedFieldMappings);
            copy.ModelChildren.AddRange(ModelChildren);
            copy.UpsertKeys.AddRange(UpsertKeys);
            copy.Diagnostics.AddRange(Diagnostics);
            copy.AutoChildAttachments.AddRange(AutoChildAttachments);
            copy.CteUpdateMeta.AddRange(CteUpdateMeta);

            return copy;
        }
    }
    
    public sealed record ForeignKeyDefinitionInfo
    {
        public required INamedTypeSymbol Entity { get; init; }

        public required string Column { get; init; }

        public required INamedTypeSymbol DependsOn { get; init; }

        public required string Principal { get; init; }

        public string? ModelField { get; init; }
    }

    public sealed class EntityKeyInfo
    {
        public string From { get; set; } = "";

        public string AliasFrom { get; set; } = "";
        
        public string? AliasProperty { get; set; }

        public string? FromColumn { get; set; }

        public string To { get; set; } = "";

        public string AliasTo { get; set; } = "";

        public string? ToColumn { get; set; }

        public required INamedTypeSymbol EntityType { get; set; }

        public bool IsPrimary { get; set; }
    }
    
    public sealed record NavigationDefinitionInfo
    {
        public required string NavigationName { get; set; }
        public INamedTypeSymbol? TargetModel { get; set; }
        public bool IsCollection { get; set; } = true;
        public List<JoinPathDefinitionInfo> Paths { get; set; } = [];
    }

    public sealed record JoinPathDefinitionInfo
    {
        public INamedTypeSymbol? TargetEntity { get; set; }
        public List<JoinHopDefinitionInfo> Hops { get; set; } = [];
    }

    public sealed record JoinHopDefinitionInfo
    {
        public INamedTypeSymbol? FromEntity { get; set; }
        public string? FromColumn { get; set; }
        public INamedTypeSymbol? ToEntity { get; set; }
        public string? ToColumn { get; set; }
    }

    public sealed class CteUpdateMetaInfo
    {
        public required string NavigationAlias { get; set; }

        public required string ForeignKeyColumn { get; set; }

        public required string OwningPrimaryKeyColumn { get; set; }

        public required string RelatedEntityTypeName { get; set; }

        public required string RelatedSurrogateIdColumn { get; set; }

        public required string RelatedNaturalKeyColumn { get; set; }
    }

    public sealed class AutoChildAttachmentInfo
    {
        public required string FieldName { get; set; }

        public required string ToModelName { get; set; }

        public required INamedTypeSymbol ParentEntityType { get; set; }

        public required string ParentJoinColumn { get; set; }

        public required INamedTypeSymbol ChildEntityType { get; set; }

        public required string ChildJoinColumn { get; set; }
    }

    public sealed class FieldInfo
    {
        public string SourceName { get; set; } = "";

        public bool IsNavigationKey { get; set; }

        public string DestinationEntity { get; set; } = "";

        public string DestinationName { get; set; } = "";

        public string? DestinationColumn { get; set; }

        public string? SourceAlias { get; set; }

        public string? DestinationAlias { get; set; }

        public INamedTypeSymbol? ModelEnumType { get; set; }

        public INamedTypeSymbol? EntityEnumType { get; set; }

        public Dictionary<string, string> EnumOverrides { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> EnumIgnored { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public bool HasEnumTranslation =>
            ModelEnumType != null &&
            EntityEnumType != null;

        public bool IsGenerated { get; set; }

        public ITypeSymbol? PropertyType { get; set; }
    }

    public sealed class ExcludedFieldMappingInfo
    {
        public required string SourceName { get; set; }

        public required string DestinationEntity { get; set; }
    }

    public sealed class ModelChildInfo
    {
        public required string From { get; set; }

        public required string To { get; set; }

        public required string NavigationName { get; set; }
    }

    public sealed class UpsertKeyInfo
    {
        public required string Entity { get; set; }

        public required string Key { get; set; }
    }

    public sealed class GraphInfo
    {
        public string GraphName { get; set; } = "";

        public string EdgeLabel { get; set; } = "";

        public string EdgeKey { get; set; } = "";

        public VertexInfo? From { get; set; }

        public VertexInfo? To { get; set; }

        public string FromJoinColumn { get; set; } = "";

        public string ToJoinColumn { get; set; } = "";
    }

    public sealed class VertexInfo
    {
        public string Label { get; set; } = string.Empty;
        public string GraphProperty { get; set; } = string.Empty;
        public string ForeignKeyColumn { get; set; } = string.Empty;
        public string KeyColumn { get; set; } = string.Empty;
        public string? Alias { get; set; }
    }

    public sealed class NavigationInfo
    {
        public required string NavigationName { get; init; }
        public INamedTypeSymbol? TargetModel { get; init; }  
        public INamedTypeSymbol? RelatedEntityType { get; init; }
        public string? ForeignKeyProperty { get; init; }     // kept for simple/alias cases
        public string? PrincipalKeyProperty { get; init; }
        public bool IsCollection { get; init; }
        public bool TargetIsRoot { get; init; }
        public bool FkOwnedByDeclaringEntity { get; init; }
        public List<NavigationJoinPath> JoinPaths { get; init; } = [];
    }
    
    public sealed class NavigationJoinPath
    {
        public required INamedTypeSymbol TargetEntity { get; init; }
        public required List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> Hops { get; init; }
    }

    public sealed class NavigationResolutionResult
    {
        public List<NavigationInfo> Navigations { get; } = new();

        public bool HasBlockingAmbiguity { get; set; }

        public List<Diagnostic> PendingDiagnostics { get; } = new();
    }
    
    

    public sealed record MappingDefinitionInfo
    {
        public INamedTypeSymbol ModelType { get; set; } = null!;

        public string? Schema { get; set; }

        public bool IsGraph { get; set; }

        public List<NavigationDefinitionInfo> Navigations { get; set; } = [];

        public List<EntityDefinitionInfo> Entities { get; set; } = [];

        public List<PrimaryKeyDefinitionInfo> PrimaryKey { get; set; } = [];

        public List<ForeignKeyDefinitionInfo> ForeignKeys { get; set; } = [];

        public GraphDefinitionInfo? Graph { get; set; }
    }
    
    public sealed record PrimaryKeyDefinitionInfo
    {
        public required INamedTypeSymbol Entity { get; set; }
        public required string ModelKey { get; set; }
        public required string ColumnKey { get; set; }
    }

    public sealed record EntityDefinitionInfo
    {
        public INamedTypeSymbol EntityType { get; set; } = null!;

        public string? FromColumn { get; set; } = string.Empty;

        public string? ToColumn { get; set; } = string.Empty;

        public string To => EntityType?.Name ?? string.Empty;

        public string? AliasProperty { get; set; }

        public bool IsPrimary { get; set; }

        // Compatibility with older emitters.
        // Represents the model-side key property.
        public string? ModelKey { get; set; }

        // Represents the entity/database column.
        public string? EntityKey { get; set; }
    }

    public sealed record FieldDefinitionInfo
    {
        public required string Source { get; set; }

        public required INamedTypeSymbol EntityType { get; set; }

        public required string Destination { get; set; }

        public EnumMappingDefinitionInfo? EnumMapping { get; set; }
    }

    public abstract record EnumMappingDefinitionInfo
    {
        public abstract INamedTypeSymbol ModelEnum { get; }

        public abstract INamedTypeSymbol EntityEnum { get; }
    }

    public sealed record GraphDefinitionInfo
    {
        public required string GraphName { get; set; }

        public required string EdgeLabel { get; set; }

        public required string EdgeKey { get; set; }

        public required VertexDefinitionInfo From { get; set; }

        public required VertexDefinitionInfo To { get; set; }

        public required string FromJoinColumn { get; set; }

        public required string ToJoinColumn { get; set; }
    }

    public sealed class VertexDefinitionInfo
    {
        public required string Label { get; set; }

        public required string KeyColumn { get; set; }

        public string? Alias { get; set; }
    }
}