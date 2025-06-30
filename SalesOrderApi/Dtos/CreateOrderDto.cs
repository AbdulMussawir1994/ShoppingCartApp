using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesOrderApi.Dtos;

public class CreateOrderDto
{
    [Required, MaxLength(50)]
    public string ProductId { get; set; } = string.Empty; // = Guid.NewGuid().ToString();
    [Required, Column(TypeName = "decimal(18,2)")]
    public decimal TotalOrders { get; set; }

}

public class OrderMessageDto
{
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ProductName { get; set; }
    public decimal TotalOrders { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
