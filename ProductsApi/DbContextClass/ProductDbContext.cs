using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ProductsApi.Models;

namespace ProductsApi.DbContextClass
{
    public class ProductDbContext : DbContext
    {
        public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options)
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
            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Product");

                entity.HasKey(e => e.ProductId);

                entity.Property(e => e.ProductId)
                    .HasMaxLength(36)
                    .IsUnicode(false);

                entity.Property(e => e.ProductName)
                    .IsRequired()
                    .HasMaxLength(30)
                    .IsUnicode(true); // Allows multilingual names

                entity.Property(e => e.ProductDescription)
                    .HasMaxLength(500)
                    .IsUnicode(true); // Descriptions may have special characters

                entity.Property(e => e.ProductCategory)
                    .HasMaxLength(30)
                    .IsUnicode(false); // Categories are often ASCII (e.g., "electronics")

                entity.Property(e => e.ProductPrice)
                    .IsRequired()
                    .HasColumnType("decimal(18,2)"); // Precise decimal handling

                entity.Property(e => e.ImageUrl)
                    .IsUnicode(false); // Base64 is plain ASCII, better stored as varchar

                // ✅ SQL Server-compatible UTC default timestamp
                entity.Property(e => e.CreatedDate)
                        .HasDefaultValueSql("GETUTCDATE()") // Ensures DB defaulting
                        .IsRequired();

                entity.Property(e => e.CreatedBy)
                .HasMaxLength(50);
            });

            //// 🌟 Seed Data
            //modelBuilder.Entity<Product>().HasData(
            //    new Product
            //    {
            //        ProductId = "1",
            //        ProductName = "Wireless Mouse",
            //        ProductDescription = "Ergonomic wireless mouse with 2.4GHz connection",
            //        ProductCategory = "Electronics",
            //        ProductPrice = 29.99m,
            //        ImageUrl = "https://example.com/images/mouse.jpg",
            //        CreatedDate = DateTime.UtcNow,
            //        CreatedBy = "Seeder"
            //    },
            //    new Product
            //    {
            //        ProductId = "2",
            //        ProductName = "Bluetooth Speaker",
            //        ProductDescription = "Portable Bluetooth speaker with HD sound",
            //        ProductCategory = "Audio",
            //        ProductPrice = 49.99m,
            //        ImageUrl = "https://example.com/images/speaker.jpg",
            //        CreatedDate = DateTime.UtcNow,
            //        CreatedBy = "Seeder"
            //    },
            //    new Product
            //    {
            //        ProductId = "3",
            //        ProductName = "Smart Watch",
            //        ProductDescription = "Fitness tracking smart watch with heart rate monitor",
            //        ProductCategory = "Wearables",
            //        ProductPrice = 99.99m,
            //        ImageUrl = "https://example.com/images/watch.jpg",
            //        CreatedDate = DateTime.UtcNow,
            //        CreatedBy = "Seeder"
            //    }
            //);
        }

        public DbSet<Product> Products => Set<Product>();
    }
}
