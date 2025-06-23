namespace ProductsApi.Dtos;

public readonly record struct GetProductDto(string id, string product, string description, string category,
                                                                                    decimal price, DateTime created, string createdBy, string image);

public class ProductDto
{
    public string Id { get; set; }
    public string ProductName { get; set; }
}
