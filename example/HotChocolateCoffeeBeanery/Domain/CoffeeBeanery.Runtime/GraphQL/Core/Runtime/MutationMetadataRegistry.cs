// #nullable enable
//
// using System;
// using System.Collections.Generic;
// using System.Collections.Immutable;
//
// namespace CoffeeBeanery.GraphQL.Core.Runtime;
//
// public static class MutationMetadataRegistry
// {
//     private static readonly MutationEntityMetadata[] _entities =
//     {
//         CreateAccount(),
//         CreateContactPoint(),
//         CreateContract(),
//         CreateCustomer(),
//         CreateCustomerBankingRelationship(),
//         CreateCustomerCustomerRelationship(),
//         CreateProduct(),
//         CreateTransaction()
//     };
//
//
//     public static MutationEntityMetadata Get(
//         ushort entityId)
//     {
//         if (entityId >= _entities.Length)
//             throw new ArgumentOutOfRangeException(nameof(entityId));
//
//         return _entities[entityId];
//     }
//
//
//     private static MutationEntityMetadata CreateAccount()
//     {
//         return new MutationEntityMetadata(
//             EntityId.Account,
//             StorageEntityId.Account,
//             "Banking",
//             "Account",
//             true,
//             MutationKind.Entity,
//             ImmutableArray.Create(
//                 "AccountKey"),
//
//             new Dictionary<ushort, MutationFieldMetadata>
//             {
//                 [FieldId.Account.AccountKey] =
//                     new(ColumnId.Account.AccountKey)
//             });
//     }
//
//
//     private static MutationEntityMetadata CreateContactPoint()
//     {
//         return new MutationEntityMetadata(
//             EntityId.ContactPoint,
//             StorageEntityId.ContactPoint,
//             "Banking",
//             "ContactPoint",
//             false,
//             MutationKind.Entity,
//             ImmutableArray.Create(
//                 "ContactPointKey"),
//
//             new Dictionary<ushort, MutationFieldMetadata>
//             {
//                 [FieldId.ContactPoint.ContactPointKey] =
//                     new(ColumnId.ContactPoint.ContactPointKey),
//
//                 [FieldId.ContactPoint.ContactPointType] =
//                     new(ColumnId.ContactPoint.ContactPointType)
//             });
//     }
//
//
//     private static MutationEntityMetadata CreateContract()
//     {
//         return new MutationEntityMetadata(
//             EntityId.Contract,
//             StorageEntityId.Contract,
//             "Lending",
//             "Contract",
//             false,
//             MutationKind.Entity,
//             ImmutableArray.Create(
//                 "ContractKey"),
//
//             new Dictionary<ushort, MutationFieldMetadata>
//             {
//                 [FieldId.Contract.ContractKey] =
//                     new(ColumnId.Contract.ContractKey)
//             });
//     }
//
//
//     private static MutationEntityMetadata CreateCustomer()
//     {
//         return new MutationEntityMetadata(
//             EntityId.Customer,
//             StorageEntityId.Customer,
//             "Banking",
//             "Customer",
//             true,
//             MutationKind.Entity,
//             ImmutableArray.Create(
//                 "CustomerKey"),
//
//             new Dictionary<ushort, MutationFieldMetadata>
//             {
//                 [FieldId.Customer.CustomerKey] =
//                     new(ColumnId.Customer.CustomerKey),
//
//                 [FieldId.Customer.CustomerType] =
//                     new(ColumnId.Customer.CustomerType),
//
//                 [FieldId.Customer.FirstNaming] =
//                     new(ColumnId.Customer.FirstName),
//
//                 [FieldId.Customer.LastNaming] =
//                     new(ColumnId.Customer.LastName),
//
//                 [FieldId.Customer.FullNaming] =
//                     new(ColumnId.Customer.FullName)
//             });
//     }
//
//
//     private static MutationEntityMetadata CreateCustomerBankingRelationship()
//     {
//         return new MutationEntityMetadata(
//             EntityId.CustomerBankingRelationship,
//             StorageEntityId.CustomerBankingRelationship,
//             "Banking",
//             "CustomerBankingRelationship",
//             false,
//             MutationKind.Entity,
//             ImmutableArray.Create(
//                 "CustomerBankingRelationshipKey"),
//
//             new Dictionary<ushort, MutationFieldMetadata>
//             {
//                 [FieldId.CustomerBankingRelationship.CustomerBankingRelationshipKey] =
//                     new(ColumnId.CustomerBankingRelationship.CustomerBankingRelationshipKey)
//             });
//     }
//
//
//     private static MutationEntityMetadata CreateCustomerCustomerRelationship()
//     {
//         return new MutationEntityMetadata(
//             EntityId.CustomerCustomerEdge,
//             StorageEntityId.CustomerCustomerRelationship,
//             "Banking",
//             "CustomerCustomerRelationship",
//             false,
//             MutationKind.GraphEdge,
//             ImmutableArray.Create(
//                 "CustomerCustomerRelationshipKey"),
//
//             new Dictionary<ushort, MutationFieldMetadata>
//             {
//                 [FieldId.CustomerCustomerEdge.CustomerCustomerRelationshipKey] =
//                     new(ColumnId.CustomerCustomerRelationship.CustomerCustomerRelationshipKey),
//
//                 [FieldId.CustomerCustomerEdge.CustomerCustomerRelationshipType] =
//                     new(ColumnId.CustomerCustomerRelationship.CustomerCustomerRelationshipType)
//             });
//     }
//
//
//     private static MutationEntityMetadata CreateProduct()
//     {
//         return new MutationEntityMetadata(
//             EntityId.Product,
//             StorageEntityId.Product,
//             "Banking",
//             "Product",
//             false,
//             MutationKind.Entity,
//             ImmutableArray.Create(
//                 "ProductKey"),
//
//             new Dictionary<ushort, MutationFieldMetadata>
//             {
//                 [FieldId.Product.AccountKey] =
//                     new(ColumnId.Account.AccountKey),
//
//                 [FieldId.Product.ContractKey] =
//                     new(ColumnId.Contract.ContractKey),
//
//                 [FieldId.Product.TransactionKey] =
//                     new(ColumnId.Transaction.TransactionKey),
//
//                 [FieldId.Product.CustomerBankingRelationshipKey] =
//                     new(ColumnId.CustomerBankingRelationship.CustomerBankingRelationshipKey),
//
//                 [FieldId.Product.ProductType] =
//                     new(ColumnId.Contract.ContractType)
//             });
//     }
//
//
//     private static MutationEntityMetadata CreateTransaction()
//     {
//         return new MutationEntityMetadata(
//             EntityId.Transaction,
//             StorageEntityId.Transaction,
//             "Lending",
//             "Transaction",
//             false,
//             MutationKind.Entity,
//             ImmutableArray.Create(
//                 "TransactionKey"),
//
//             new Dictionary<ushort, MutationFieldMetadata>
//             {
//                 [FieldId.Transaction.TransactionKey] =
//                     new(ColumnId.Transaction.TransactionKey)
//             });
//     }
// }