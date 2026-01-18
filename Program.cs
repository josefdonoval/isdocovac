using Isdocovac.Components;
using Isdocovac.Data;
using Isdocovac.Providers;
using Isdocovac.Services;
using Isdocovac.Services.Authentication;
using Isdocovac.Services.Email;
using Isdocovac.Services.Fakturoid;
using Isdocovac.Services.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

// Register Entity Framework DbContext with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")),
    ServiceLifetime.Scoped);

// Register application providers
builder.Services.AddScoped<IUserProvider, UserProvider>();
builder.Services.AddScoped<IMainInvoiceProvider, MainInvoiceProvider>();
builder.Services.AddScoped<IParsedInvoiceProvider, ParsedInvoiceProvider>();
builder.Services.AddScoped<IParsedInvoiceProcessingProvider, ParsedInvoiceProcessingProvider>();
builder.Services.AddScoped<IAzureBlobStorageProvider, AzureBlobStorageProvider>();

// Legacy providers (will be removed after migration)
builder.Services.AddScoped<IInvoiceProvider, InvoiceProvider>();
builder.Services.AddScoped<IParsedIsdocProvider, ParsedIsdocProvider>();
builder.Services.AddScoped<IInvoiceProcessingProvider, InvoiceProcessingProvider>();

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

// Register application services
builder.Services.AddScoped<IInvoiceImportService, InvoiceImportService>();
builder.Services.AddScoped<IInvoiceManagementService, InvoiceManagementService>();
builder.Services.AddScoped<IParsedInvoiceService, ParsedInvoiceService>();

// Legacy services (will be removed after migration)
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

var app = builder.Build();

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

app.Run();
