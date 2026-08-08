using CoffeeBeanery.GraphQL.Core.Sql;

namespace Domain.Model;

public class CustomerCustomerRelationship : IModel
{
    public Customer? InnerCustomer { get; set; }
    
    public Guid? InnerCustomerKey { get; set; }
    
    public Customer? OuterCustomer { get; set; }
    
    public Guid? OuterCustomerKey { get; set; }

    public string CustomerCustomerRelationshipKey { get; set; }
    public CustomerCustomerRelationshipType? CustomerCustomerRelationshipType { get; set; }
    
}
public enum CustomerCustomerRelationshipType
{
    Family,
    Partner,
    Widow,
    Single,
    Divorced
}