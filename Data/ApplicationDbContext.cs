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
    public DbSet<InvoiceProcessing> InvoiceProcessings { get; set; }
    public DbSet<AuthenticationToken> AuthenticationTokens { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    public DbSet<LoginAttempt> LoginAttempts { get; set; }

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
            entity.Property(e => e.DisplayName).HasMaxLength(255);
            entity.Property(e => e.EmailVerified).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
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
            entity.Property(e => e.Status).IsRequired().HasConversion<int>();
            entity.Property(e => e.BlobContainerName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.BlobName).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.BlobUrl).HasMaxLength(2048);
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

        // InvoiceProcessing configuration
        modelBuilder.Entity<InvoiceProcessing>(entity =>
        {
            entity.ToTable("invoice_processings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InvoiceUploadId);
            entity.HasIndex(e => e.ParsedIsdocId);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Status).IsRequired().HasConversion<int>();
            entity.Property(e => e.StartedAt).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.AttemptNumber).IsRequired();
            entity.HasOne(e => e.InvoiceUpload)
                .WithMany(i => i.Processings)
                .HasForeignKey(e => e.InvoiceUploadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ParsedIsdoc)
                .WithMany()
                .HasForeignKey(e => e.ParsedIsdocId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // AuthenticationToken configuration
        modelBuilder.Entity<AuthenticationToken>(entity =>
        {
            entity.ToTable("authentication_tokens");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => new { e.Email, e.ExpiresAt });
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.IsRevoked).IsRequired();
        });

        // UserSession configuration
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionTokenHash).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.IsActive });
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.SessionTokenHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired();
            entity.HasOne(e => e.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // LoginAttempt configuration
        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.ToTable("login_attempts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Email, e.AttemptedAt });
            entity.HasIndex(e => new { e.IpAddress, e.AttemptedAt });
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.AttemptedAt).IsRequired();
            entity.Property(e => e.WasSuccessful).IsRequired();
            entity.Property(e => e.FailureReason).HasMaxLength(500);
        });
    }
}
