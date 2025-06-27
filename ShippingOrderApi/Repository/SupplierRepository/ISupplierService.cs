using ShippingOrderApi.Dtos;
using ShippingOrderApi.Utilities;
using ShippingOrderApi.ViewModel;

namespace ShippingOrderApi.Repository.SupplierRepository
{
    public interface ISupplierService
    {
        Task<MobileResponse<SupplierGetDto>> CreateSupplierAsync(SupplierCreateDto model, CancellationToken ctx);
        Task<MobileResponse<string>> ConfirmDeliveryAsync(DispatchViewModel model);
    }
}
