namespace Database.Graph;

public class CustomerCustomerEdge
{
    public int OuterCustomerId { get; set; }
    
    public int InnerCustomerId { get; set; }
    
    public int Id { get; set; }
}