using ShippingOrderApi.Model;

namespace ShippingOrderApi.Dtos;

public class ShippingDto
{
    public string Consumer { get; set; }
    public string UserId { get; set; }
    public string OrderId { get; set; }
    public ShippingAddress Address { get; set; } = new ShippingAddress();
}
