using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ShippingOrderApi.Model;

namespace ShippingOrderApi.DbContextClass;

public class ShippingDbContext : DbContext
{
    //When Posgres Installed then run command through query tool
    //  ALTER DATABASE template1 REFRESH COLLATION VERSION;
    public ShippingDbContext(DbContextOptions<ShippingDbContext> options) : base(options)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        //  AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

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
        base.OnModelCreating(modelBuilder);

        // Supplier Table
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Supplier");
            entity.HasKey(e => e.SupplierId);

            entity.Property(e => e.SupplierId)
                  .UseIdentityColumn(); // PostgreSQL: GENERATED ALWAYS AS IDENTITY

            entity.Property(e => e.SupplierName)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.HasIndex(e => e.SupplierName).HasDatabaseName("IX_Supplier_Name");
        });

        // ShippingAddress Table
        modelBuilder.Entity<ShippingAddress>(entity =>
        {
            entity.ToTable("ShippingAddress");
            entity.HasKey(e => e.ShippingId);

            entity.Property(e => e.ShippingId)
                  .UseIdentityColumn();

            entity.Property(e => e.HomeAddress)
                  .IsRequired()
                  .HasMaxLength(255);

            entity.Property(e => e.City)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.Region)
                  .HasMaxLength(100);

            entity.Property(e => e.Country)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.Phone)
                  .HasMaxLength(20);

            entity.HasOne(sa => sa.Supplier)
                  .WithMany(s => s.ShippingAddresses)
                  .HasForeignKey(sa => sa.SupplierId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(sa => sa.City).HasDatabaseName("IX_Shipping_City");
        });
    }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ShippingAddress> ShippingAddresses => Set<ShippingAddress>();

}
