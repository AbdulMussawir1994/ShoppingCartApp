using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductsApi.Dtos;

public class SelectProductDto
{
    [Required, MaxLength(50)]
    public string ProductId { get; set; } = string.Empty;
    [Required, Column(TypeName = "decimal(18,2)")]
    public decimal TotalOrders { get; set; }
}
