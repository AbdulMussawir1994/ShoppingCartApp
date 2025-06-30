namespace SalesOrderApi.Dtos.Address;

public class ShippingResponseDto
{
    public string Consumer { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public AddressDto Address { get; set; } = new AddressDto();
}
