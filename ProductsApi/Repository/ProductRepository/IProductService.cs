using ProductsApi.Dtos;
using ProductsApi.Utilities;

namespace ProductsApi.Repository.ProductRepository;

public interface IProductService
{
    Task<MobileResponse<IEnumerable<GetProductDto>>> GetProductsAsync(CancellationToken ctx);
    Task<MobileResponse<IEnumerable<GetProductDto>>> GetAllAsync(CancellationToken ctx);
    Task<MobileResponse<GetProductDto>> GetByIdAsync(string id, CancellationToken ctx);
    Task<MobileResponse<GetProductDto>> CreateAsync(CreateProductDto model, CancellationToken ctx);
    Task<MobileResponse<GetProductDto>> UpdateAsync(string id, CreateProductDto model, CancellationToken ctx);
    Task<MobileResponse<bool>> DeleteAsync(string id, CancellationToken ctx);
    Task<MobileResponse<ProductDto>> GetProductNameAsync(string Id, CancellationToken ctx);
    Task<MobileResponse<ProductMessageDto>> SelectProductAsync(SelectProductDto model, CancellationToken ctx);
}