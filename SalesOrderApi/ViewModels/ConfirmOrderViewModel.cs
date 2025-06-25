using SalesOrderApi.Dtos.Address;

namespace SalesOrderApi.ViewModels;

public class ConfirmOrderViewModel
{
    public int OrderId { get; set; }
    public AddressDto Address { get; set; } = new AddressDto();
}