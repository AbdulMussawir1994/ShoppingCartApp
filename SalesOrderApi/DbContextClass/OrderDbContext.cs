using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using SalesOrderApi.Model;

namespace SalesOrderApi.DbContextClass
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
        {
            try
            {
                var databaseCreator = Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                if (databaseCreator != null)
                {
                    if (!databaseCreator.CanConnect()) databaseCreator.Create();
                    if (!databaseCreator.HasTables()) databaseCreator.CreateTables();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("Order");

                entity.HasKey(e => e.OrderId);
                entity.HasIndex(u => u.OrderId).IsUnique().HasDatabaseName("IDX_OrderId)");

                entity.Property(e => e.ProductId)
                    .HasMaxLength(36)
                    .IsUnicode(false);

                entity.Property(e => e.ProductName)
                    .IsRequired()
                    .HasMaxLength(30)
                    .IsUnicode(true); // Allows multilingual names

                // ✅ MySQL-compatible UTC default timestamp
                entity.Property(e => e.CreatedDate)
                                 .HasColumnType("timestamp")
                                 .HasDefaultValueSql("CURRENT_TIMESTAMP")
                                 .IsRequired();

            });

            //// 🌟 Seed Data
            //modelBuilder.Entity<OrderDetails>().HasData(
            //    new OrderDetails
            //    {
            //        OrderDetailsId = 1,
            //        ProductId = "1",
            //        ProductName = "Wireless Mouse",
            //        Stock = 5,
            //        Price = 29.99m,
            //        CreatedDate = DateTime.UtcNow,
            //    },
            //    new OrderDetails
            //    {
            //        OrderDetailsId = 2,
            //        ProductId = "2",
            //        ProductName = "Bluetooth Speaker",
            //        Stock = 5,
            //        Price = 49.99m,
            //        CreatedDate = DateTime.UtcNow,
            //    },
            //    new OrderDetails
            //    {
            //        OrderDetailsId = 3,
            //        ProductId = "3",
            //        ProductName = "Smart Watch",
            //        Stock = 5,
            //        Price = 99.99m,
            //        CreatedDate = DateTime.UtcNow,
            //    }
            //);
        }

        public DbSet<Order> Orders => Set<Order>();
        public DbSet<ConfirmOrder> ConfirmOrders => Set<ConfirmOrder>();
    }
}
