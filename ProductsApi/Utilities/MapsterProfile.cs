using Mapster;
using ProductsApi.Dtos;
using ProductsApi.Models;

namespace ProductsApi.Utilities;

public sealed class MapsterProfile : TypeAdapterConfig
{
    public MapsterProfile()
    {
        EmployeeMapping();
    }

    private void EmployeeMapping()
    {
        TypeAdapterConfig<Product, GetProductDto>.NewConfig()
            .Map(x => x.id, map => map.ProductId)
            .Map(x => x.product, map => map.ProductName)
            .Map(x => x.description, map => map.ProductDescription)
            .Map(x => x.category, map => map.ProductCategory)
            .Map(x => x.price, map => map.ProductPrice)
            .Map(x => x.created, map => map.CreatedDate)
            .Map(x => x.createdBy, map => map.CreatedBy)
            .Map(x => x.image, map => map.ImageUrl)
            .IgnoreNullValues(true);

        TypeAdapterConfig<CreateProductDto, Product>.NewConfig()
            .Map(x => x.ProductName, map => map.ProductName)
            .Map(x => x.ProductDescription, map => map.ProductDescription)
            .Map(x => x.ProductCategory, map => map.ProductCategory)
            .Map(x => x.ProductPrice, map => map.ProductPrice)
            .Map(x => x.ImageUrl, map => map.ImageUrl)
           .IgnoreNullValues(true);
    }
}
