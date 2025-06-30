using SalesOrderApi.Dtos;
using SalesOrderApi.Utilities;
using SalesOrderApi.ViewModels;

namespace SalesOrderApi.Repository.OrderRepository
{
    public interface IOrderService
    {
        //Task<MobileResponse<string>> ConfirmOrderByIdInQueueAsync(ConfirmOrderViewModel model);
        Task<string> UpdateConfirmOrderDetails(string queueName);
        Task<MobileResponse<string>> ConfirmOrderByIdAsync(ConfirmOrderViewModel model);
        Task<MobileResponse<IEnumerable<GetOrderDto>>> GetAllAsync(CancellationToken ctx);
        Task<MobileResponse<GetOrderDto>> GetByIdAsync(string id, CancellationToken ctx);
        Task<MobileResponse<OrderMessageDto>> CreateAsync(CreateOrderDto model, CancellationToken ctx);
        Task<MobileResponse<GetOrderDto>> UpdateAsync(string id, CreateOrderDto model, CancellationToken ctx);
        Task<MobileResponse<bool>> DeleteAsync(string id, CancellationToken ctx);
        Task<MobileResponse<OrderMessageDto>> ProductConfirmAsync(ConfirmProductViewModel model);
    }
}
