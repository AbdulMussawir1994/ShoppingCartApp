using SalesOrderApi.Dtos;
using SalesOrderApi.Utilities;
using SalesOrderApi.ViewModels;

namespace SalesOrderApi.Repository.OrderRepository
{
    public interface IOrderService
    {
        //Task<MobileResponse<string>> ConfirmOrderByIdInQueueAsync(ConfirmOrderViewModel model);
        Task<string> UpdateConfirmOrderDetails(string queueName);
        Task<MobileResponse<IEnumerable<GetOrderDto>>> GetAllAsync(CancellationToken ctx);
        Task<MobileResponse<GetOrderDto>> GetByIdAsync(int id, CancellationToken ctx);
        Task<MobileResponse<OrderMessageDto>> CreateAsync(CreateOrderDto model, CancellationToken ctx);
        Task<MobileResponse<GetOrderDto>> UpdateAsync(int id, CreateOrderDto model, CancellationToken ctx);
        Task<MobileResponse<bool>> DeleteAsync(int id, CancellationToken ctx);
        Task<MobileResponse<OrderMessageDto>> ProductConfirmAsync(ConfirmProductViewModel model);
    }
}
