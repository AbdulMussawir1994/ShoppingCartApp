using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShippingOrderApi.Model;


public class ShippingAddress
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ShippingId { get; set; }

    [Required]
    public string Consumer { get; set; }
    [Required]
    public string UserId { get; set; }
    [Required]
    public int OrderId { get; set; }

    [Required]
    [MaxLength(255)]
    public string HomeAddress { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Region { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    [MaxLength(50)]
    public string? CreatedBy { get; set; } = string.Empty; // UserId
    public string? UpdatedBy { get; set; } = string.Empty;

    [ForeignKey(nameof(Supplier))]
    public int SupplierId { get; set; }

    public virtual Supplier Supplier { get; set; } = null!;
}
