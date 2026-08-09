using CoffeeBeanery.GraphQL.Core.Sql;

namespace Domain.Model;

public class CustomerBankingRelationship : IModel
{
    
    public Guid? CustomerBankingRelationshipKey { get; set; }
    public CustomerCustomerRelationshipType? CustomerCustomerRelationshipType { get; set; }
    
    public Guid? CustomerKey { get; set; }
    
    public List<Contract>? Contract { get; set; }
}