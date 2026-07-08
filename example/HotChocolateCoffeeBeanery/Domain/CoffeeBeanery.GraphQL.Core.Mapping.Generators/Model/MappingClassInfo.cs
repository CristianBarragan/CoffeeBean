using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model
{
    public sealed class MappingClassInfo
    {
        public INamedTypeSymbol ClassSymbol { get; set; } = null!;

        // Graph/model type
        public INamedTypeSymbol ModelType { get; set; } = null!;

        // Primary Entity type for this mapping
        public INamedTypeSymbol? EntityType { get; set; }


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



        /// <summary>
        /// All explicit Model -> Entity mappings.
        ///
        /// Example:
        ///
        /// CustomerCustomerEdge
        ///
        ///   -> CustomerCustomerRelationship
        ///   -> Customer (InnerCustomer)
        ///   -> Customer (OuterCustomer)
        ///
        /// </summary>
        public List<EntityKeyInfo> ModelToEntity { get; }
            = new();



        /// <summary>
        /// Unique Entity symbols referenced by ModelToEntity.
        /// </summary>
        public List<INamedTypeSymbol> ModelToEntityTypes { get; }
            = new();



        public List<FieldInfo> FieldMaps { get; }
            = new();


        public List<FieldInfo> ManualFieldMaps { get; }
            = new();


        public List<ExcludedFieldMappingInfo> ExcludedFieldMappings { get; }
            = new();


        public List<ModelChildInfo> ModelChildren { get; }
            = new();


        public List<UpsertKeyInfo> UpsertKeys { get; }
            = new();


        public List<Diagnostic> Diagnostics { get; }
            = new();



        public List<AutoChildAttachmentInfo> AutoChildAttachments { get; }
            = new();



        public List<CteUpdateMetaInfo> CteUpdateMeta { get; }
            = new();



        public string Id { get; set; } = "";
    }



    public sealed class EntityKeyInfo
    {
        public string From { get; set; } = "";

        public string AliasFrom { get; set; } = "";


        public string? FromColumn { get; set; }


        public string To { get; set; } = "";

        public string AliasTo { get; set; } = "";


        public string? ToColumn { get; set; }


        // Entity type for this mapping entry
        public required INamedTypeSymbol EntityType { get; set; }


        // Model navigation alias:
        //
        // InnerCustomer
        // OuterCustomer
        //
        public string? AliasProperty { get; set; }


        public bool IsPrimary { get; set; }
    }



    public sealed class CteUpdateMetaInfo
    {
        public required string NavigationAlias { get; init; }

        public required string ForeignKeyColumn { get; init; }

        public required string OwningPrimaryKeyColumn { get; init; }

        public required string RelatedEntityTypeName { get; init; }

        public required string RelatedSurrogateIdColumn { get; init; }

        public required string RelatedNaturalKeyColumn { get; init; }
    }



    public sealed class AutoChildAttachmentInfo
    {
        public required string FieldName { get; init; }

        public required string ToModelName { get; init; }

        public required INamedTypeSymbol ParentEntityType { get; init; }

        public required string ParentJoinColumn { get; init; }

        public required INamedTypeSymbol ChildEntityType { get; init; }

        public required string ChildJoinColumn { get; init; }
    }



    public sealed class FieldInfo
    {
        public string SourceName { get; set; }

        public string DestinationEntity { get; set; }

        public string DestinationName { get; set; }

        public string? SourceAlias { get; set; }

        public string? DestinationAlias { get; set; }

        public Dictionary<string, int>? FromEnum { get; set; }

        public Dictionary<string, int>? ToEnum { get; set; }

        public Dictionary<string,string> EnumOverrides { get; } = [];

        public HashSet<string> EnumIgnore { get; } = [];
        
        public bool IsGenerated { get; set; }
    }



    public sealed class ExcludedFieldMappingInfo
    {
        public required string SourceName { get; init; }

        public required string DestinationEntity { get; init; }
    }



    public sealed class ModelChildInfo
    {
        public required string To { get; init; }
    }



    public sealed class UpsertKeyInfo
    {
        public required string Entity { get; init; }

        public required string Key { get; init; }
    }

    public sealed class GraphInfo
    {
        public string GraphName { get; set; }
        
        public string EdgeLabel { get; set; }
        
        public string EdgeKey { get; set; }
        
        public VertexInfo From { get; set; }
        
        public VertexInfo To { get; set; }
        
        public string FromJoinColumn { get; set; }
        
        public string ToJoinColumn { get; set; }
    }

    public sealed class VertexInfo
    {
        public string Label { get; set; } = string.Empty;

        public string KeyColumn { get; set; } = string.Empty;

        public string? Alias { get; set; }
    }

    public sealed class NavigationInfo
    {
        public required string NavigationName { get; init; }

        public required INamedTypeSymbol RelatedEntityType { get; init; }

        public required string ForeignKeyProperty { get; init; }

        public required string PrincipalKeyProperty { get; init; }

        public bool IsCollection { get; init; }

        public bool TargetIsRoot { get; set; }
    }



    public sealed class NavigationResolutionResult
    {
        public List<NavigationInfo> Navigations { get; }
            = new();


        public bool HasBlockingAmbiguity { get; set; }


        public List<Diagnostic> PendingDiagnostics { get; }
            = new();
    }
}