using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ShippingOrderApi.Dtos
{
    public class SupplierCreateDto
    {
        [Required]
        [MaxLength(200)]
        [DisplayName("Name")]
        public string SupplierName { get; set; } = string.Empty;
    }
}
