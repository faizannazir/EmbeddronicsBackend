using Microsoft.EntityFrameworkCore;
using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Data;

public class EmbeddronicsDbContext : DbContext
{
    public EmbeddronicsDbContext(DbContextOptions<EmbeddronicsDbContext> options) : base(options)
    {
    }

    // DbSets for all entities
    public DbSet<User> Users { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<QuoteItem> QuoteItems { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<Invoice> Invoices { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50).HasDefaultValue("client");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("pending");
            entity.Property(e => e.Company).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure Order entity
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("new");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.PcbSpecs).HasColumnType("nvarchar(max)");
            entity.Property(e => e.BudgetRange).HasMaxLength(100);
            entity.Property(e => e.Timeline).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Configure relationships
            entity.HasOne(e => e.Client)
                  .WithMany(u => u.Orders)
                  .HasForeignKey(e => e.ClientId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Quote entity
        modelBuilder.Entity<Quote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("USD");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("pending");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Configure relationships
            entity.HasOne(e => e.Client)
                  .WithMany(u => u.Quotes)
                  .HasForeignKey(e => e.ClientId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Quotes)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure QuoteItem entity
        modelBuilder.Entity<QuoteItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)").IsRequired();

            // Configure relationships
            entity.HasOne(e => e.Quote)
                  .WithMany(q => q.Items)
                  .HasForeignKey(e => e.QuoteId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Product)
                  .WithMany(p => p.QuoteItems)
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure Message entity
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Attachments).HasColumnType("nvarchar(max)");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Configure relationships
            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Messages)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Sender)
                  .WithMany(u => u.Messages)
                  .HasForeignKey(e => e.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Product entity
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.Features).HasColumnType("nvarchar(max)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure Document entity
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Configure relationships
            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Documents)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.UploadedBy)
                  .WithMany()
                  .HasForeignKey(e => e.UploadedById)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure Invoice entity
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("USD");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("pending");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Configure relationships
            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Invoices)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Quote)
                  .WithMany()
                  .HasForeignKey(e => e.QuoteId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Ensure unique invoice numbers
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
        });
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is User || e.Entity is Order || e.Entity is Quote || 
                       e.Entity is Product || e.Entity is Invoice)
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Property("CreatedAt").CurrentValue == null)
                    entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
            }

            if (entry.Property("UpdatedAt") != null)
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
        }
    }
}