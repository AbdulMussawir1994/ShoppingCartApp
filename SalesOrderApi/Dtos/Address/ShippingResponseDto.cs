namespace SalesOrderApi.Dtos.Address;

public class ShippingResponseDto
{
    public string Consumer { get; set; }
    public string UserId { get; set; }
    public int OrderId { get; set; }
    public AddressDto Address { get; set; } = new AddressDto();
}
