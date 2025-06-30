using SalesOrderApi.Dtos.Address;

namespace SalesOrderApi.ViewModels;

public class ConfirmOrderViewModel
{
    public string OrderId { get; set; } = string.Empty;
    public AddressDto Address { get; set; } = new AddressDto();
}


public class ConfirmProductViewModel
{
    public string OrderId { get; set; } = string.Empty;
}