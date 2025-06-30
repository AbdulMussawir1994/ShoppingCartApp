using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesOrderApi.Model;

public class Order
{
    //[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public string OrderId { get; set; } = string.Empty; // = Guid.NewGuid().ToString();
    [Required, MaxLength(50)]
    public string ProductId { get; set; } = string.Empty; // = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string ProductName { get; set; }

    [Required, Column(TypeName = "decimal(18,2)")]
    public decimal TotalOrders { get; set; }

    [Display(Name = "Consumer Name")]
    public string Consumer { get; set; } = string.Empty;
    [Display(Name = "Order Status")]
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    //  public string Queue { get; set; } = string.Empty;

    public virtual ICollection<ConfirmOrder> ConfirmOrders { get; private set; } = new List<ConfirmOrder>();
}
