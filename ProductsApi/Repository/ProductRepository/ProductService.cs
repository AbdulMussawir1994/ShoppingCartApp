using Mapster;
using Microsoft.EntityFrameworkCore;
using ProductsApi.DbContextClass;
using ProductsApi.Dtos;
using ProductsApi.Helpers;
using ProductsApi.Models;
using ProductsApi.Repository.RabbitMqProducer;
using ProductsApi.Repository.UserContext;
using ProductsApi.Utilities;

namespace ProductsApi.Repository.ProductRepository;

public class ProductService : IProductService
{
    private readonly ProductDbContext _db;
    private readonly IUserService _contextUser;
    private readonly IRabbitMqService _rabbitMqService;
    // private readonly SnowflakeIdGenerator _idGenerator;

    public ProductService(ProductDbContext db, IUserService contextUser, IRabbitMqService rabbitMqService)
    {
        _db = db;
        _contextUser = contextUser;
        _rabbitMqService = rabbitMqService;
        // _idGenerator = idGenerator;
    }

    public async Task<MobileResponse<IEnumerable<GetProductDto>>> GetAllAsync(CancellationToken ctx)
    {
        try
        {
            var products = await _db.Products.AsNoTracking().ToListAsync(ctx);

            if (!products.Any())
                return MobileResponse<IEnumerable<GetProductDto>>.EmptySuccess(Enumerable.Empty<GetProductDto>(), "No Products Found");

            var mapped = products.Adapt<IEnumerable<GetProductDto>>();
            return MobileResponse<IEnumerable<GetProductDto>>.Success(mapped, "Products Fetched Successfully");
        }
        catch (Exception ex)
        {
            return MobileResponse<IEnumerable<GetProductDto>>.Fail($"An error occurred: {ex.Message}");
        }
    }

    public async Task<MobileResponse<GetProductDto>> GetByIdAsync(string id, CancellationToken ctx)
    {
        try
        {
            var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id, ctx);

            return product is null
                ? MobileResponse<GetProductDto>.Fail("Product Not Found")
                : MobileResponse<GetProductDto>.Success(product.Adapt<GetProductDto>(), "Product Fetched Successfully");
        }
        catch (Exception ex)
        {
            return MobileResponse<GetProductDto>.Fail($"An error occurred: {ex.Message}");
        }
    }

    public async Task<MobileResponse<GetProductDto>> CreateAsync(CreateProductDto model, CancellationToken ctx)
    {
        try
        {
            var product = model.Adapt<Product>();
            product.CreatedBy = _contextUser.UserId;

            await _db.Products.AddAsync(product, ctx);
            var result = await _db.SaveChangesAsync(ctx);

            return result > 0
                ? MobileResponse<GetProductDto>.Success(product.Adapt<GetProductDto>(), "Product Created Successfully.")
                : MobileResponse<GetProductDto>.Fail("Failed to Create Product");
        }
        catch (Exception ex)
        {
            return MobileResponse<GetProductDto>.Fail($"An error occurred: {ex.Message}");
        }
    }

    public async Task<MobileResponse<GetProductDto>> UpdateAsync(string id, CreateProductDto model, CancellationToken ctx)
    {
        try
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == id, ctx);

            if (product is null)
                return MobileResponse<GetProductDto>.Fail("Product Not Found");

            model.Adapt(product); // Efficiently map updated fields to entity

            _db.Products.Update(product);
            var result = await _db.SaveChangesAsync(ctx);

            return result > 0
                ? MobileResponse<GetProductDto>.Success(product.Adapt<GetProductDto>(), "Product Updated Successfully")
                : MobileResponse<GetProductDto>.Fail("Failed to Update Product");
        }
        catch (Exception ex)
        {
            return MobileResponse<GetProductDto>.Fail($"An error occurred: {ex.Message}");
        }
    }

    public async Task<MobileResponse<bool>> DeleteAsync(string id, CancellationToken ctx)
    {
        try
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == id, ctx);

            if (product is null)
                return MobileResponse<bool>.Fail("Product Not Found");

            _db.Products.Remove(product);
            var result = await _db.SaveChangesAsync(ctx);

            return result > 0
                ? MobileResponse<bool>.EmptySuccess(true, "Product Deleted Successfully")
                : MobileResponse<bool>.Fail("Failed to Delete Product");
        }
        catch (Exception ex)
        {
            return MobileResponse<bool>.Fail($"An error occurred: {ex.Message}");
        }
    }

    public async Task<MobileResponse<IEnumerable<GetProductDto>>> GetProductsAsync(CancellationToken ctx)
    {
        try
        {
            var response = await _db.Products.AsNoTracking().ToListAsync(ctx);

            var products = response.Adapt<IEnumerable<GetProductDto>>();

            return response.Any()
                ? MobileResponse<IEnumerable<GetProductDto>>.Success(products, "Products Fetched Successfully")
                : MobileResponse<IEnumerable<GetProductDto>>.EmptySuccess(Enumerable.Empty<GetProductDto>(), "No Employees Found.");
        }
        catch (Exception ex)
        {
            return MobileResponse<IEnumerable<GetProductDto>>.Fail($"An error Occured: {ex.Message}", "400");
        }
    }

    public async Task<MobileResponse<ProductDto>> GetProductNameAsync(string Id, CancellationToken ctx)
    {
        try
        {
            var product = await _db.Products
                .AsNoTracking()
                .Where(x => x.ProductId == Id)
                .Select(x => new ProductDto
                {
                    Id = x.ProductId,
                    ProductName = x.ProductName
                })
                .FirstOrDefaultAsync(ctx);

            if (product is not ProductDto validProduct)
                return MobileResponse<ProductDto>.Fail("Product Not Found", "404");

            return MobileResponse<ProductDto>.Success(validProduct, "Product Fetched Successfully", "200");
        }
        catch (Exception ex)
        {
            return MobileResponse<ProductDto>.Fail($"An error occurred: {ex.Message}", "500");
        }
    }

    public async Task<MobileResponse<ProductMessageDto>> SelectProductAsync(SelectProductDto model, CancellationToken ctx)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProductId == model.ProductId, ctx);

        if (product is null)
            return MobileResponse<ProductMessageDto>.Fail("Product Not Found", "404");

        // ✅ Prepare RabbitMQ message
        var rabbitMqOrder = new ProductMessageDto
        {
            OrderId = NumberGenerator.GenerateSixDigitNumberWithGuid(),
            ProductId = product.ProductId,
            UserId = _contextUser?.UserId ?? "1",
            ProductName = product.ProductName,
            Consumer = _contextUser?.Email ?? "Not Found",
            Status = "Pending",
            TotalOrders = model.TotalOrders,
        };

        // ✅ Start a transaction
        await using var transaction = await _db.Database.BeginTransactionAsync(ctx);
        try
        {
            var productConfirmed = new ProductConfirmed
            {
                ProductId = product.ProductId,
                UserId = _contextUser?.UserId ?? "1",
                GenerateSixDigitNumberId = rabbitMqOrder.OrderId,
                TotalOrders = model.TotalOrders,
            };

            await _db.ProductConfirmed.AddAsync(productConfirmed, ctx);
            var saved = await _db.SaveChangesAsync(ctx);

            if (saved == 0)
            {
                await transaction.RollbackAsync(ctx);
                return MobileResponse<ProductMessageDto>.Fail("Database save failed. Transaction rolled back.", "500");
            }

            var published = _rabbitMqService.PublishMessageWithReturn("OrderQueue", rabbitMqOrder);
            if (!published)
            {
                await transaction.RollbackAsync(ctx);
                return MobileResponse<ProductMessageDto>.Fail("Message publish failed. Transaction rolled back.", "500");
            }

            await transaction.CommitAsync(ctx);
            return MobileResponse<ProductMessageDto>.Success(rabbitMqOrder, "Order created and message published successfully.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ctx);
            return MobileResponse<ProductMessageDto>.Fail("Transaction failed due to an error.", "500");
        }
    }
}
