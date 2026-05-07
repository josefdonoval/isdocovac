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
    public DbSet<Company> Companies { get; set; }

    // Main Invoice Tables
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceLine> InvoiceLines { get; set; }
    public DbSet<InvoicePayment> InvoicePayments { get; set; }
    public DbSet<InvoiceAttachment> InvoiceAttachments { get; set; }

    // ISDOC Upload/Parsing (Staging)
    public DbSet<ParsedInvoice> ParsedInvoices { get; set; }
    public DbSet<ParsedInvoiceProcessing> ParsedInvoiceProcessings { get; set; }

    // Fakturoid Integration (Staging)
    public DbSet<FakturoidConnection> FakturoidConnections { get; set; }
    public DbSet<FakturoidOAuthState> FakturoidOAuthStates { get; set; }
    public DbSet<FakturoidInvoice> FakturoidInvoices { get; set; }
    public DbSet<FakturoidInvoiceLine> FakturoidInvoiceLines { get; set; }
    public DbSet<FakturoidInvoicePayment> FakturoidInvoicePayments { get; set; }
    public DbSet<FakturoidInvoiceAttachment> FakturoidInvoiceAttachments { get; set; }

    // Authentication
    public DbSet<AuthenticationToken> AuthenticationTokens { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    public DbSet<LoginAttempt> LoginAttempts { get; set; }

    // Contacts + VAT reporting
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<FxRate> FxRates { get; set; }

    // Investments
    public DbSet<OptionTrade> OptionTrades { get; set; }
    public DbSet<Share> Shares { get; set; }
    public DbSet<ShareQuote> ShareQuotes { get; set; }
    public DbSet<BrokerImport> BrokerImports { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // NOTE: Most entity configurations are now defined using attributes on the model classes
        // This section only contains configurations that cannot be expressed via attributes

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
            entity.Property(e => e.ActiveCompanyId);
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

        // Company configuration
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("companies");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OwnerUserId);
            entity.HasIndex(e => new { e.OwnerUserId, e.IsActive });
            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // FakturoidConnection configuration
        modelBuilder.Entity<FakturoidConnection>(entity =>
        {
            entity.ToTable("fakturoid_connections");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CompanyId).IsUnique();
            entity.HasIndex(e => e.AccountSlug);
            entity.HasIndex(e => new { e.CompanyId, e.IsActive });
            entity.Property(e => e.AccessToken).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.RefreshToken).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.AccountSlug).IsRequired().HasMaxLength(255);
            entity.Property(e => e.AccountName).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.ConnectedAt).IsRequired();
            entity.Property(e => e.AccessTokenExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // FakturoidOAuthState configuration
        modelBuilder.Entity<FakturoidOAuthState>(entity =>
        {
            entity.ToTable("fakturoid_oauth_states");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CompanyId, e.StateHash }).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.StateHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Soft delete filters — propagate to child entities so the "required end" warnings disappear
        modelBuilder.Entity<Invoice>().HasQueryFilter(i => !i.IsDeleted);
        modelBuilder.Entity<ParsedInvoice>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<InvoiceLine>().HasQueryFilter(x => !x.Invoice.IsDeleted);
        modelBuilder.Entity<InvoicePayment>().HasQueryFilter(x => !x.Invoice.IsDeleted);
        modelBuilder.Entity<InvoiceAttachment>().HasQueryFilter(x => !x.Invoice.IsDeleted);
        modelBuilder.Entity<ParsedInvoiceProcessing>().HasQueryFilter(x => !x.ParsedInvoice.IsDeleted);

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => new { e.CompanyId, e.InternalNumber }).IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.OriginalDocumentNumber });
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Distinct from ParsedInvoice.ImportedInvoice — silences the "separated into two relationships" warning.
            entity.HasOne(e => e.SourceParsedInvoice)
                .WithMany()
                .HasForeignKey(e => e.ParsedInvoiceId);
        });

        modelBuilder.Entity<ParsedInvoice>(entity =>
        {
            entity.HasIndex(e => e.CompanyId);
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ImportedInvoice)
                .WithMany()
                .HasForeignKey(e => e.ImportedInvoiceId);
        });

        // Contact configuration
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => new { e.CompanyId, e.Kind });
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // FxRate configuration
        modelBuilder.Entity<FxRate>(entity =>
        {
            entity.HasIndex(e => new { e.Date, e.Currency }).IsUnique();
        });

        // OptionTrade configuration
        modelBuilder.Entity<OptionTrade>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyId, e.TradeDateTime });
            entity.HasIndex(e => new { e.CompanyId, e.Symbol });
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Share configuration
        modelBuilder.Entity<Share>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyId, e.TradeDateTime });
            entity.HasIndex(e => new { e.CompanyId, e.Symbol });
            entity.HasIndex(e => e.BrokerImportId);
            // Non-unique: brokers (e.g. Degiro) reuse a single Order ID across multi-fill rows.
            // Dedup happens at the application layer in BrokerImportService.
            entity.HasIndex(e => new { e.CompanyId, e.Broker, e.ExternalOrderId });
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.BrokerImport)
                .WithMany()
                .HasForeignKey(e => e.BrokerImportId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // BrokerImport configuration
        modelBuilder.Entity<BrokerImport>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyId, e.UploadedAt });
            entity.HasIndex(e => new { e.CompanyId, e.FileHash }).IsUnique();
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ShareQuote configuration
        modelBuilder.Entity<ShareQuote>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyId, e.Symbol }).IsUnique();
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
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
