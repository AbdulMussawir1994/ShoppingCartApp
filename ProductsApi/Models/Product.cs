using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductsApi.Models;

//[Table("Product", Schema = "dbo")]
public class Product
{
    [Key]
    [MaxLength(36)]
    public string ProductId { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(30)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ProductDescription { get; set; }

    [MaxLength(30)]
    public string? ProductCategory { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ProductPrice { get; set; }

    public string? ImageUrl { get; set; } // Base64, no MaxLength since it's large

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    [MaxLength(50)]
    public string? CreatedBy { get; set; } = string.Empty; // UserId
    public string? UpdatedBy { get; set; } = string.Empty;



}
