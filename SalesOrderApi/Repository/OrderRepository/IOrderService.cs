using SalesOrderApi.Dtos;
using SalesOrderApi.Utilities;

namespace SalesOrderApi.Repository.OrderRepository
{
    public interface IOrderService
    {
        Task<MobileResponse<IEnumerable<GetOrderDto>>> GetAllAsync(CancellationToken ctx);
        Task<MobileResponse<GetOrderDto>> GetByIdAsync(int id, CancellationToken ctx);
        Task<MobileResponse<GetOrderDto>> CreateAsync(CreateOrderDto model, CancellationToken ctx);
        Task<MobileResponse<GetOrderDto>> UpdateAsync(int id, CreateOrderDto model, CancellationToken ctx);
        Task<MobileResponse<bool>> DeleteAsync(int id, CancellationToken ctx);
    }
}
