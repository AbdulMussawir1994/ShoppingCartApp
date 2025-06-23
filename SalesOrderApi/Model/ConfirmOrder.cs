using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesOrderApi.Model
{
    public class ConfirmOrder
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ConfirmOrderId { get; set; }
        [Required, MaxLength(50)]
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime ConfirmedOrder { get; set; }

        [Required]
        public int OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public virtual Order Orders { get; set; } = null!;
    }
}
