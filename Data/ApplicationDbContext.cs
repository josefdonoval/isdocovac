using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<InvoiceUpload> InvoiceUploads { get; set; }
    public DbSet<ParsedIsdoc> ParsedIsdocs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.PasswordSalt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        // InvoiceUpload configuration
        modelBuilder.Entity<InvoiceUpload>(entity =>
        {
            entity.ToTable("invoice_uploads");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.UploadedAt);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.RawXmlContent).IsRequired();
            entity.HasOne(e => e.User)
                .WithMany(u => u.InvoiceUploads)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ParsedIsdoc configuration
        modelBuilder.Entity<ParsedIsdoc>(entity =>
        {
            entity.ToTable("parsed_isdocs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InvoiceUploadId);
            entity.HasIndex(e => e.ParsedAt);
            entity.Property(e => e.ParsedData).IsRequired();
            entity.Property(e => e.InvoiceNumber).HasMaxLength(100);
            entity.Property(e => e.SupplierName).HasMaxLength(255);
            entity.Property(e => e.CustomerName).HasMaxLength(255);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.InvoiceUpload)
                .WithMany(i => i.ParsedIsdocs)
                .HasForeignKey(e => e.InvoiceUploadId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
