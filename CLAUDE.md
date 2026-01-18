# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **Blazor Server** application built with **.NET 10.0** using interactive server-side rendering. The application uses the new Blazor Web App template architecture with component-based development.

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the application (development mode)
dotnet run

# Run with specific configuration
dotnet run --configuration Release

# Watch mode (auto-rebuild on changes)
dotnet watch

# Clean build artifacts
dotnet clean
```

## Project Structure and Architecture

### Core Application Setup

- **[Program.cs](Program.cs)**: Application entry point and service configuration
  - Configures Razor Components with Interactive Server rendering mode
  - Sets up antiforgery, HTTPS redirection, and static assets
  - Implements custom status code pages with `/not-found` route reexecution
  - Registers application services via dependency injection (add new services here)
  - Important: `BlazorDisableThrowNavigationException` is enabled in project settings

### Component Organization

- **[Components/App.razor](Components/App.razor)**: Root HTML document that hosts the Blazor application
  - Uses `@Assets` helper for static file references
  - Includes `<ReconnectModal />` component for SignalR reconnection handling
  - Bootstrap CSS framework is included via lib directory

- **[Components/Routes.razor](Components/Routes.razor)**: Router configuration
  - Uses `typeof(Program).Assembly` for route discovery
  - `NotFoundPage` points to custom `Pages.NotFound` component
  - Default layout is `Layout.MainLayout`

- **[Components/_Imports.razor](Components/_Imports.razor)**: Global using statements for all Razor components
  - Pre-configured with common Blazor namespaces
  - Includes static import for `Microsoft.AspNetCore.Components.Web.RenderMode`

### Layout Structure

- **Components/Layout/**: Contains layout components
  - `MainLayout.razor`: Standard sidebar + main content layout with navigation
  - `NavMenu.razor`: Application navigation menu
  - `ReconnectModal.razor`: Custom SignalR reconnection UI with JavaScript interop

### Pages

All pages are in **Components/Pages/** with `@page` directive for routing:
- `Home.razor`: Root page (`/`)
- `Counter.razor`: Example interactive counter component
- `Weather.razor`: Example data display component (uses dependency injection for `IWeatherService`)
- `Error.razor`: Error handling page
- `NotFound.razor`: 404 error page

### Business Logic Layer

**Code Organization**: Interfaces and implementations are co-located in the same file. Each provider/service file contains both the interface definition (at the top) and its implementation class.

- **Services/**: Contains business logic services
  - Services are registered in [Program.cs](Program.cs) using dependency injection
  - Use `@inject` directive in Razor components to access services
  - **Authentication Services** (`Services/Authentication/`):
    - `MagicLinkService`: Magic link token generation and validation
    - `SessionService`: User session management
  - **Email Services** (`Services/Email/`):
    - `LoopsEmailService`: Email delivery via Loops API
  - **Security Services** (`Services/Security/`):
    - `RateLimitService`: Rate limiting for authentication attempts
  - **Fakturoid Services** (`Services/Fakturoid/`):
    - `FakturoidOAuthService`: OAuth flow with Fakturoid API
    - `FakturoidApiService`: Fakturoid API integration for fetching invoices
    - `FakturoidSyncService`: Invoice synchronization from Fakturoid

- **Providers/**: Data access layer wrapping Entity Framework DbContext
  - Providers handle all database operations and are injected into services/components
  - **Authentication Providers**:
    - `UserProvider`: User management and authentication
    - `AuthTokenProvider`: Magic link token storage and validation
    - `LoginAttemptProvider`: Login attempt tracking for rate limiting
    - `SessionProvider`: User session storage (uses IDbContextFactory)
  - **Invoice Providers**:
    - `InvoiceProvider`: Invoice upload management with Azure Blob Storage integration
    - `ParsedIsdocProvider`: Parsed ISDOC data management
    - `InvoiceProcessingProvider`: Invoice processing workflow
  - **Fakturoid Providers**:
    - `FakturoidConnectionProvider`: Fakturoid OAuth connection management
    - `FakturoidInvoiceProvider`: Fakturoid invoice data storage
    - `FakturoidOAuthStateProvider`: OAuth state verification
  - **Storage Providers**:
    - `AzureBlobStorageProvider`: Azure Blob Storage file operations

- **Models/**: Contains Entity Framework database models
  - `User.cs`: User entity with authentication fields (Id, Username, Email, PasswordHash, PasswordSalt, timestamps)
  - `InvoiceUpload.cs`: Invoice upload entity with Azure Blob Storage references (BlobContainerName, BlobName, BlobUrl)
  - `ParsedIsdoc.cs`: Parsed invoice data extracted from ISDOC XML
  - `InvoiceUploadStatus.cs`: Enum for upload status (Pending, Processing, Completed, Failed)
  - `WeatherForecast.cs`: Example model for weather data (not in database)

### Static Assets

- **wwwroot/**: Static file directory
  - `app.css`: Application-specific styles
  - `lib/`: Third-party libraries (Bootstrap)
  - `favicon.png`: Application icon

## Render Modes

This project uses **Interactive Server** render mode (configured in [Program.cs:25](Program.cs#L25)). Components can specify render mode using `@rendermode` directive. The `_Imports.razor` includes a static import of `RenderMode` so you can use `@rendermode InteractiveServer` directly.

## Database Architecture

### Entity Framework Core with PostgreSQL

The application uses **Entity Framework Core 9.0** with **PostgreSQL** (Npgsql provider).

**DbContext**: [Data/ApplicationDbContext.cs](Data/ApplicationDbContext.cs)
- Manages three main entities: Users, InvoiceUploads, and ParsedIsdocs
- Configured with proper indexes, constraints, and relationships

**Entity Relationships**:
- `User` (1) → (many) `InvoiceUpload` - Users own their invoice uploads
- `InvoiceUpload` (1) → (many) `ParsedIsdoc` - Each upload can have multiple parsed versions

**Database Schema**:
- `users` table: User accounts with authentication credentials
- `invoice_uploads` table: Stores invoice upload metadata and Azure Blob Storage references
- `parsed_isdocs` table: Stores parsed invoice data extracted from XML

### Database Migrations

```bash
# Create a new migration
dotnet ef migrations add MigrationName --output-dir Data/Migrations

# Apply migrations to database
dotnet ef database update

# Rollback to specific migration
dotnet ef database update PreviousMigrationName

# Remove last migration (if not applied)
dotnet ef migrations remove
```

### Azure Blob Storage Integration

The application uses **Azure Blob Storage** for storing uploaded ISDOC XML files. Files are NOT stored in the database, only metadata and blob references are stored.

**Configuration**:
- Connection string in `appsettings.json` under `AzureStorage:ConnectionString`
- Container name configured as `AzureStorage:InvoiceContainerName` (default: `invoice-uploads`)
- Update connection string with your Azure Storage account credentials

**Blob Storage Structure**:
- Files are organized by user and upload ID: `{userId}/{uploadId}/{fileName}`
- Private container access (no public access)
- Secure access via Shared Access Signature (SAS) URLs for temporary file access

**AzureBlobStorageProvider Methods**:
- `UploadBlobAsync()`: Upload file to blob storage
- `DownloadBlobAsync()`: Download file from blob storage
- `DeleteBlobAsync()`: Delete file from blob storage
- `BlobExistsAsync()`: Check if blob exists
- `GetBlobUrl()`: Get blob URL
- `GenerateSasUrlAsync()`: Generate time-limited SAS URL for secure file access

### ISDOC Format

ISDOC (Invoice Standard Document) is an XML-based invoice format recognized by the Czech Ministry of Finance. The application:
- Accepts ISDOC XML uploads (conforming to https://isdoc.cz/6.0.2/xsd/isdoc-invoice-6.0.2.xsd)
- Stores ISDOC XML files in Azure Blob Storage (not in database)
- Database stores only metadata and blob references (container name, blob name, URL)
- Parses and validates XML, storing results in `ParsedIsdoc` entities
- Supports multiple parse versions per upload for re-parsing scenarios

## Configuration

- **[appsettings.json](appsettings.json)**: Main configuration file
- **[appsettings.Development.json](appsettings.Development.json)**: Development-specific settings

**Required Configuration**:
1. **Database Connection String**: `Host=localhost;Database=isdocovac_db;Username=postgres;Password=YOUR_PASSWORD_HERE`
   - Update password before running migrations
2. **Azure Storage Connection String**: `YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE`
   - Format: `DefaultEndpointsProtocol=https;AccountName=<account>;AccountKey=<key>;EndpointSuffix=core.windows.net`
   - Required for file upload functionality
3. **Azure Storage Container Name**: Configured as `AzureStorage:InvoiceContainerName`
   - Production: `invoice-uploads`
   - Development: `invoice-uploads-dev`

- Logging levels are configured for ASP.NET Core components

## Development Notes

- **Solution file**: Uses `.slnx` format (XML-based solution file)
- **Target Framework**: .NET 10.0
- **Nullable reference types**: Enabled
- **Implicit usings**: Enabled
- SignalR connection handling is customized via `ReconnectModal` component with JavaScript interop
