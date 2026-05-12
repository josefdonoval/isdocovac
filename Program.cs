using Isdocovac.Components;
using Isdocovac.Data;
using Isdocovac.Providers;
using Isdocovac.Providers.Email;
using Isdocovac.Providers.Inbox;
using Isdocovac.Services;
using Isdocovac.Services.Authentication;
using Isdocovac.Services.Email;
using Isdocovac.Services.Email.Ingestion;
using Isdocovac.Services.Fakturoid;
using Isdocovac.Services.Fx;
using Isdocovac.Services.ISDOC;
using Isdocovac.Services.Vat;
using Isdocovac.Providers.Fx;
using Isdocovac.Providers.Investments;
using Isdocovac.Services.Investments;
using Isdocovac.Services.Investments.BrokerImport;
using Isdocovac.Services.Claude;
using Isdocovac.Services.Ares;
using Isdocovac.Services.CodeLists;
using Isdocovac.Services.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// All Serilog configuration is driven from the "Serilog" section in appsettings.json.
builder.Host.UseSerilog((context, services, loggerConfig) => loggerConfig
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

// Add custom cookie-based authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "SessionToken";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(
            builder.Configuration.GetValue<int>("Authentication:Session:AbsoluteExpirationDays", 14));
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register Entity Framework DbContext with PostgreSQL.
// DbContextFactory is registered as singleton (default) so its options are not
// captured from a scoped IServiceProvider that may be disposed mid-flow.
// Scoped ApplicationDbContext is produced via the factory so existing
// constructor-injected providers keep working.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ApplicationDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

// Register application providers
builder.Services.AddScoped<IUserProvider, UserProvider>();
builder.Services.AddScoped<IMainInvoiceProvider, MainInvoiceProvider>();
builder.Services.AddScoped<IInvoiceAttachmentProvider, InvoiceAttachmentProvider>();
builder.Services.AddScoped<IParsedInvoiceProvider, ParsedInvoiceProvider>();
builder.Services.AddScoped<IParsedInvoiceProcessingProvider, ParsedInvoiceProcessingProvider>();
builder.Services.AddScoped<IAzureBlobStorageProvider, AzureBlobStorageProvider>();

// Register authentication providers
builder.Services.AddScoped<IAuthTokenProvider, AuthTokenProvider>();
builder.Services.AddScoped<ISessionProvider, SessionProvider>();
builder.Services.AddScoped<ILoginAttemptProvider, LoginAttemptProvider>();

// Register Fakturoid providers
builder.Services.AddScoped<IFakturoidConnectionProvider, FakturoidConnectionProvider>();
builder.Services.AddScoped<IFakturoidInvoiceProvider, FakturoidInvoiceProvider>();
builder.Services.AddScoped<IFakturoidOAuthStateProvider, FakturoidOAuthStateProvider>();

// Register Fakturoid services
builder.Services.AddScoped<IFakturoidOAuthService, FakturoidOAuthService>();
builder.Services.AddScoped<IFakturoidApiService, FakturoidApiService>();
builder.Services.AddScoped<IFakturoidSyncService, FakturoidSyncService>();

// Register authentication services
builder.Services.AddScoped<IMagicLinkService, MagicLinkService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IEmailService, LoopsEmailService>();
builder.Services.AddScoped<IRateLimitService, RateLimitService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

// Add HTTP context accessor for authentication
builder.Services.AddHttpContextAccessor();

// Add memory cache for rate limiting
builder.Services.AddMemoryCache();

// Add HTTP client for email service
builder.Services.AddHttpClient();

// Add HTTP client for Claude
builder.Services.AddHttpClient("Claude");

// Register Claude invoice parsing service
builder.Services.AddScoped<IInvoiceParsingService, ClaudeInvoiceParsingService>();

// Register ISDOC services
builder.Services.AddScoped<IIsdocGeneratorService, IsdocGeneratorService>();
builder.Services.AddScoped<IIsdocValidationService, IsdocValidationService>();
builder.Services.AddScoped<IIsdocXmlParsingService, IsdocXmlParsingService>();

// Register PDF processing services
builder.Services.AddScoped<IPdfInvoiceProcessingService, PdfInvoiceProcessingService>();

// Register application services
builder.Services.AddScoped<IInvoiceImportService, InvoiceImportService>();
builder.Services.AddScoped<IInvoiceManagementService, InvoiceManagementService>();
builder.Services.AddScoped<IParsedInvoiceService, ParsedInvoiceService>();

// Companies + multi-company support
builder.Services.AddScoped<ICompanyProvider, CompanyProvider>();
builder.Services.AddScoped<ICurrentCompanyAccessor, CurrentCompanyAccessor>();

// Contacts + VAT reporting
builder.Services.AddScoped<IContactProvider, ContactProvider>();
builder.Services.AddScoped<IFxRateProvider, FxRateProvider>();
builder.Services.AddHttpClient("Cnb");
builder.Services.AddScoped<ICnbExchangeRateService, CnbExchangeRateService>();
builder.Services.AddSingleton<ICodeListService, CodeListService>();
builder.Services.AddHttpClient("Ares");
builder.Services.AddScoped<IAresLookupService, AresLookupService>();
builder.Services.AddScoped<IVatCalculationService, VatCalculationService>();
builder.Services.AddScoped<IDphXmlGeneratorService, DphXmlGeneratorService>();
builder.Services.AddScoped<IKhXmlGeneratorService, KhXmlGeneratorService>();

// Investments
builder.Services.AddScoped<IOptionTradeProvider, OptionTradeProvider>();
builder.Services.AddScoped<IOptionTradeCalculationService, OptionTradeCalculationService>();
builder.Services.AddScoped<IShareProvider, ShareProvider>();
builder.Services.AddScoped<IShareQuoteProvider, ShareQuoteProvider>();
builder.Services.AddScoped<IShareCalculationService, ShareCalculationService>();
builder.Services.AddHttpClient("Yahoo");
builder.Services.AddScoped<IShareQuoteService, YahooQuoteService>();
builder.Services.AddHttpClient("OpenFigi");
builder.Services.AddScoped<ISecurityLookupService, OpenFigiSecurityLookupService>();

// Broker imports (CSV/XLSX from Degiro, XTB, …)
builder.Services.AddScoped<IBrokerImportProvider, BrokerImportProvider>();
builder.Services.AddScoped<IBrokerImportParser, DegiroCsvParser>();
builder.Services.AddScoped<IBrokerImportParser, XtbXlsxParser>();
builder.Services.AddScoped<ISymbolResolutionService, SymbolResolutionService>();
builder.Services.AddScoped<IBrokerImportService, BrokerImportService>();

// Email ingestion ("desk")
builder.Services.Configure<EmailIngestionOptions>(
    builder.Configuration.GetSection(EmailIngestionOptions.SectionName));
builder.Services.AddSingleton<IPasswordCipher, AesGcmPasswordCipher>();
builder.Services.AddScoped<IMailboxAccountProvider, MailboxAccountProvider>();
builder.Services.AddScoped<IEmailIngestionMessageProvider, EmailIngestionMessageProvider>();
builder.Services.AddScoped<IExternalOriginFileProvider, ExternalOriginFileProvider>();
builder.Services.AddScoped<IImapMailboxClient, MailKitImapMailboxClient>();
builder.Services.AddScoped<IEmailIngestionService, EmailIngestionService>();
// BackgroundService gates itself off when EmailIngestion:Enabled is false (local debug).
builder.Services.AddHostedService<EmailIngestionWorker>();

var app = builder.Build();

// Apply pending EF Core migrations at startup. Fails fast on a misconfigured DB so we don't
// serve traffic against a stale schema. Safe to call when there's nothing to do.
await using (var migrationScope = app.Services.CreateAsyncScope())
{
    var contextFactory = migrationScope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    await using var migrationContext = await contextFactory.CreateDbContextAsync();
    var pending = (await migrationContext.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        Log.Information("Applying {Count} pending EF migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await migrationContext.Database.MigrateAsync();
        Log.Information("EF migrations applied.");
    }
    else
    {
        Log.Information("Database schema up to date — no pending migrations.");
    }
}

// One log line per HTTP request: method, path, status, elapsed, plus user/session enrichment.
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("RequestHost", http.Request.Host.Value ?? string.Empty);
        diag.Set("RequestScheme", http.Request.Scheme);
        diag.Set("UserAgent", http.Request.Headers.UserAgent.ToString());
        if (http.User?.Identity?.IsAuthenticated == true)
        {
            diag.Set("UserName", http.User.Identity.Name ?? string.Empty);
            var uid = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(uid)) diag.Set("UserId", uid);
        }
    };
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
