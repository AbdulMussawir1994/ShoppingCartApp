using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShippingOrderApi.Model;

public class Supplier
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int SupplierId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SupplierName { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    [MaxLength(50)]
    public string? CreatedBy { get; set; } = string.Empty; // UserId
    public string? UpdatedBy { get; set; } = string.Empty;

    public virtual ICollection<ShippingAddress> ShippingAddresses { get; set; } = new HashSet<ShippingAddress>();
}