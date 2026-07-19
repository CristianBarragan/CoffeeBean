 using CoffeeBeanery.GraphQL.Core.Runtime;

 namespace CofpfeeBeanery.GraphQL.Core.Runtime;

 using System;
 using System.Collections.Generic;
 using System.Collections.Immutable;

  public sealed class MutationEntityMetadata
  {
      public ushort StorageEntityId { get; }

      public string Schema { get; }

      public string Table { get; }

      public ImmutableArray<string> Keys { get; }

      public ImmutableDictionary<ushort, MutationFieldMetadata> Fields { get; }


      public MutationEntityMetadata(
          ushort storageEntityId,
          string schema,
          string table,
          ImmutableArray<string> keys,
          ImmutableDictionary<ushort, MutationFieldMetadata> fields)
      {
          StorageEntityId = storageEntityId;
          Schema = schema;
          Table = table;
          Keys = keys;
          Fields = fields;
      }
  }