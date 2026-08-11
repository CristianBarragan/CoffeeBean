using CoffeeBeanery.GraphQL.Core.Sql;

namespace Domain.Model;

public class CustomerCustomerEdge : IModel 
{
    public Guid? CustomerCustomerRelationshipKey { get; set; }
    
    public CustomerCustomerRelationship? CustomerCustomerRelationship { get; set; }

    public Guid? OuterCustomerKey { get; set; }

    public Customer? OuterCustomer { get; set; }

    public Guid? InnerCustomerKey { get; set; }
    
    public Customer? InnerCustomer { get; set; }

    public GraphModel? GraphModel { get; set; }
}