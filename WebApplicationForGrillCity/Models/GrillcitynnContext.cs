using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace WebApplicationForGrillCity.Models;

public partial class GrillcitynnContext : DbContext
{
    public GrillcitynnContext()
    {
    }

    public GrillcitynnContext(DbContextOptions<GrillcitynnContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Discount> Discounts { get; set; }

    public virtual DbSet<Myorder> Myorders { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<Orderproduct> Orderproducts { get; set; }

    public virtual DbSet<Orderstatus> Orderstatuses { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductMovement> ProductMovements { get; set; }

    public virtual DbSet<ProductType> ProductTypes { get; set; }

    public virtual DbSet<Provider> Providers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=grillcitynn;Username=postgres;Password=123");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Discount>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("discounts_pkey");

            entity.ToTable("discounts");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DiscountPercent).HasColumnName("discount_percent");
        });

        modelBuilder.Entity<Myorder>(entity =>
        {
            entity.HasKey(e => e.Orderid).HasName("myorder_pkey");

            entity.ToTable("myorder");

            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.Clientid).HasColumnName("clientid");
            entity.Property(e => e.Codefortakeproduct).HasColumnName("codefortakeproduct");
            entity.Property(e => e.Dateoforder)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("dateoforder");
            entity.Property(e => e.Orderstatus).HasColumnName("orderstatus");

            entity.HasOne(d => d.Client).WithMany(p => p.Myorders)
                .HasForeignKey(d => d.Clientid)
                .HasConstraintName("myorder_clientid_fkey");

            entity.HasOne(d => d.OrderstatusNavigation).WithMany(p => p.Myorders)
                .HasForeignKey(d => d.Orderstatus)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("myorder_orderstatus_fkey");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orders_pkey");

            entity.ToTable("orders");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DateOfOrder).HasColumnName("date_of_order");
            entity.Property(e => e.DiscountId).HasColumnName("discount_id");
            entity.Property(e => e.FinalPrice).HasColumnName("final_price");
            entity.Property(e => e.ProductId).HasColumnName("product_id");

            entity.HasOne(d => d.Discount).WithMany(p => p.Orders)
                .HasForeignKey(d => d.DiscountId)
                .HasConstraintName("orders_discount_id_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.Orders)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("orders_product_id_fkey");
        });

        modelBuilder.Entity<Orderproduct>(entity =>
        {
            entity.HasKey(e => new { e.Orderid, e.Productsid }).HasName("orderproduct_pkey");

            entity.ToTable("orderproduct");

            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.Productsid).HasColumnName("productsid");
            entity.Property(e => e.Countinorder).HasColumnName("countinorder");

            entity.HasOne(d => d.Order).WithMany(p => p.Orderproducts)
                .HasForeignKey(d => d.Orderid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderproduct_orderid_fkey");

            entity.HasOne(d => d.Products).WithMany(p => p.Orderproducts)
                .HasForeignKey(d => d.Productsid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderproduct_productsid_fkey");
        });

        modelBuilder.Entity<Orderstatus>(entity =>
        {
            entity.HasKey(e => e.Orderstatusid).HasName("orderstatus_pkey");

            entity.ToTable("orderstatus");

            entity.Property(e => e.Orderstatusid).HasColumnName("orderstatusid");
            entity.Property(e => e.Statusname).HasColumnName("statusname");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("products_pkey");

            entity.ToTable("products");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Photo).HasColumnName("photo");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.ProductName)
                .HasMaxLength(200)
                .HasColumnName("product__name");
            entity.Property(e => e.ProductTypeId).HasColumnName("product_type_id");
            entity.Property(e => e.ProviderId).HasColumnName("provider_id");
            entity.Property(e => e.QuantityInStock).HasColumnName("quantity_in_stock");

            entity.HasOne(d => d.ProductType).WithMany(p => p.Products)
                .HasForeignKey(d => d.ProductTypeId)
                .HasConstraintName("products_product_type_id_fkey");

            entity.HasOne(d => d.Provider).WithMany(p => p.Products)
                .HasForeignKey(d => d.ProviderId)
                .HasConstraintName("products_provider_id_fkey");
        });

        modelBuilder.Entity<ProductMovement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_movements_pkey");

            entity.ToTable("product_movements");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MovementDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("movement_date");
            entity.Property(e => e.MovementType).HasColumnName("movement_type");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductMovements)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("product_movements_product_id_fkey");
        });

        modelBuilder.Entity<ProductType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_type_pkey");

            entity.ToTable("product_type");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TypeName)
                .HasMaxLength(100)
                .HasColumnName("type_name");
        });

        modelBuilder.Entity<Provider>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("providers_pkey");

            entity.ToTable("providers");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProviderName)
                .HasMaxLength(50)
                .HasColumnName("provider_name");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Userid).HasName("users_pkey");

            entity.ToTable("users");

            entity.Property(e => e.Userid).HasColumnName("userid");
            entity.Property(e => e.Fname)
                .HasMaxLength(100)
                .HasColumnName("fname");
            entity.Property(e => e.Patronumic)
                .HasMaxLength(100)
                .HasColumnName("patronumic");
            entity.Property(e => e.Phonenumber).HasColumnName("phonenumber");
            entity.Property(e => e.Sname)
                .HasMaxLength(100)
                .HasColumnName("sname");
            entity.Property(e => e.Userlogin).HasColumnName("userlogin");
            entity.Property(e => e.Userpassword).HasColumnName("userpassword");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
