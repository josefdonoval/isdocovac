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
    public DbSet<FakturoidConnection> FakturoidConnections { get; set; }
    public DbSet<FakturoidOAuthState> FakturoidOAuthStates { get; set; }
    public DbSet<FakturoidInvoice> FakturoidInvoices { get; set; }
    public DbSet<FakturoidInvoiceLine> FakturoidInvoiceLines { get; set; }
    public DbSet<FakturoidInvoicePayment> FakturoidInvoicePayments { get; set; }
    public DbSet<FakturoidInvoiceAttachment> FakturoidInvoiceAttachments { get; set; }

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

        // FakturoidConnection configuration
        modelBuilder.Entity<FakturoidConnection>(entity =>
        {
            entity.ToTable("fakturoid_connections");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.AccountSlug);
            entity.HasIndex(e => new { e.UserId, e.IsActive });
            entity.Property(e => e.AccessToken).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.RefreshToken).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.AccountSlug).IsRequired().HasMaxLength(255);
            entity.Property(e => e.AccountName).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.ConnectedAt).IsRequired();
            entity.Property(e => e.AccessTokenExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // FakturoidOAuthState configuration
        modelBuilder.Entity<FakturoidOAuthState>(entity =>
        {
            entity.ToTable("fakturoid_oauth_states");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.StateHash }).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.StateHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // FakturoidInvoice configuration
        modelBuilder.Entity<FakturoidInvoice>(entity =>
        {
            entity.ToTable("fakturoid_invoices");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FakturoidConnectionId);
            entity.HasIndex(e => e.FakturoidId);
            entity.HasIndex(e => new { e.FakturoidConnectionId, e.FakturoidId }).IsUnique();
            entity.HasIndex(e => e.UpdatedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.IssuedOn);
            entity.HasIndex(e => e.DueOn);
            entity.HasIndex(e => new { e.FakturoidConnectionId, e.LastSyncedAt });

            entity.Property(e => e.Number).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Token).HasMaxLength(100);
            entity.Property(e => e.DocumentType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(10);
            entity.Property(e => e.ClientName).HasMaxLength(500);
            entity.Property(e => e.ClientStreet).HasMaxLength(500);
            entity.Property(e => e.ClientCity).HasMaxLength(255);
            entity.Property(e => e.ClientZip).HasMaxLength(20);
            entity.Property(e => e.ClientCountry).HasMaxLength(100);
            entity.Property(e => e.ClientRegistrationNo).HasMaxLength(100);
            entity.Property(e => e.ClientVatNo).HasMaxLength(100);
            entity.Property(e => e.YourName).HasMaxLength(500);
            entity.Property(e => e.VariableSymbol).HasMaxLength(50);
            entity.Property(e => e.HtmlUrl).HasMaxLength(1000);
            entity.Property(e => e.PublicHtmlUrl).HasMaxLength(1000);

            entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Total).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RemainingAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NativeSubtotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NativeTotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NativeRemainingAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");

            entity.Property(e => e.Tags).HasColumnType("jsonb");
            entity.Property(e => e.VatRatesSummary).HasColumnType("jsonb");
            entity.Property(e => e.NativeVatRatesSummary).HasColumnType("jsonb");
            entity.Property(e => e.PaidAdvances).HasColumnType("jsonb");

            entity.HasOne(e => e.Connection)
                .WithMany(c => c.Invoices)
                .HasForeignKey(e => e.FakturoidConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // FakturoidInvoiceLine configuration
        modelBuilder.Entity<FakturoidInvoiceLine>(entity =>
        {
            entity.ToTable("fakturoid_invoice_lines");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FakturoidInvoiceId);
            entity.HasIndex(e => new { e.FakturoidInvoiceId, e.LineOrder });

            entity.Property(e => e.Name).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.UnitName).HasMaxLength(50);
            entity.Property(e => e.Sku).HasMaxLength(100);
            entity.Property(e => e.LineOrder).IsRequired();

            entity.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.VatRate).HasColumnType("decimal(5,2)");
            entity.Property(e => e.UnitPriceWithoutVat).HasColumnType("decimal(18,2)");
            entity.Property(e => e.UnitPriceWithVat).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalPriceWithoutVat).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalVat).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalPriceWithVat).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Lines)
                .HasForeignKey(e => e.FakturoidInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // FakturoidInvoicePayment configuration
        modelBuilder.Entity<FakturoidInvoicePayment>(entity =>
        {
            entity.ToTable("fakturoid_invoice_payments");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FakturoidInvoiceId);
            entity.HasIndex(e => e.FakturoidPaymentId);
            entity.HasIndex(e => new { e.FakturoidInvoiceId, e.FakturoidPaymentId }).IsUnique();
            entity.HasIndex(e => e.PaidOn);

            entity.Property(e => e.Currency).IsRequired().HasMaxLength(10);
            entity.Property(e => e.VariableSymbol).HasMaxLength(50);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.NativeAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PaidOn).IsRequired();
            entity.Property(e => e.ImportedAt).IsRequired();

            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(e => e.FakturoidInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // FakturoidInvoiceAttachment configuration
        modelBuilder.Entity<FakturoidInvoiceAttachment>(entity =>
        {
            entity.ToTable("fakturoid_invoice_attachments");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FakturoidInvoiceId);

            entity.Property(e => e.Filename).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DownloadUrl).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.ImportedAt).IsRequired();

            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Attachments)
                .HasForeignKey(e => e.FakturoidInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
