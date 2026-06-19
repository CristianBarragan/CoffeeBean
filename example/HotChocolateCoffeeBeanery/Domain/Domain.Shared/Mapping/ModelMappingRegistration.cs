using CoffeeBeanery.GraphQL.Core.Mapping;

namespace Domain.Shared.Mapping
{
    public class ModelMappingRegistration
    {
        private static readonly IMappingSet[] Sets =
        {
            new AccountMappingSet(),
            new ContactPointMappingSet(),
            new ContractMappingSet(),
            new CustomerBankingRelationshipMappingSet(),
            new CustomerMappingSet(),
            new ProductMappingSet(),
            new TransactionMappingSet(),
            new CustomerCustomerEdgeMappingSet(),
            new WrapperMappingSet()
        };
    }
}