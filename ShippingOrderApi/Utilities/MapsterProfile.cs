using Mapster;
using ShippingOrderApi.Dtos;
using ShippingOrderApi.Model;

namespace ShippingOrderApi.Utilities;

public sealed class MapsterProfile : TypeAdapterConfig
{
    public MapsterProfile()
    {
        DataMapping();
    }

    private void DataMapping()
    {
        TypeAdapterConfig<Supplier, SupplierGetDto>.NewConfig()
            .Map(x => x.id, map => map.SupplierId)
            .Map(x => x.name, map => map.SupplierName)
            .IgnoreNullValues(true);

        TypeAdapterConfig<SupplierCreateDto, Supplier>.NewConfig()
            .Map(x => x.SupplierName, map => map.SupplierName)
           .IgnoreNullValues(true);
    }
}
