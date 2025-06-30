using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductsApi.Models
{
    public class ProductConfirmed
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; }
        public string GenerateSixDigitNumberId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalOrders { get; set; }

        [Required]
        public string ProductId { get; set; }
        [ForeignKey(nameof(ProductId))]
        public virtual Product Products { get; set; } = null!;
    }
}
