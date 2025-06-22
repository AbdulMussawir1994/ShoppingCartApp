using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductsApi.Dtos;

public class CreateProductDto
{
    [Required, MaxLength(30)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ProductDescription { get; set; }

    [MaxLength(30)]
    public string? ProductCategory { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ProductPrice { get; set; }
    public IFormFile? ImageUrl { get; set; }
}