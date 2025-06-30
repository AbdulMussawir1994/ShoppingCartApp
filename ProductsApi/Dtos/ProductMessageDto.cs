namespace ProductsApi.Dtos;

public class ProductMessageDto
{
    //  public string OrderId { get; set; } = Guid.NewGuid().ToString();
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ProductName { get; set; }
    public decimal TotalOrders { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
