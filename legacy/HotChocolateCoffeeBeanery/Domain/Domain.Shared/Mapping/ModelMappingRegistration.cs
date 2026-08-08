using CoffeeBeanery.GraphQL.Core.Mapping;

namespace Domain.Shared.Mapping
{
    public class ModelMappingRegistration
    {
        private static readonly IMappingSet<CustomerMappingType, Domain.Model.Model>[] Sets =
        {
            new AccountMappingSet(),
            new ContactPointMappingSet(),
            new ContractMappingSet(),
            new CustomerBankingRelationshipMappingSet(),
            new CustomerCustomerRelationshipMappingSet(),
            new InnerCustomerMappingSet(),
            new OuterCustomerMappingSet(),
            new ProductMappingSet(),
            new TransactionMappingSet(),
            new CustomerCustomerEdgeMappingSet()
        };
    }
    
    
    public enum CustomerMappingType {
        InnerCustomer,
        OuterCustomer
    }
}