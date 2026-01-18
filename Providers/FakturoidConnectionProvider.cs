using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IFakturoidConnectionProvider
{
    Task<FakturoidConnection?> GetByUserIdAsync(Guid userId);
    Task<FakturoidConnection?> GetByIdAsync(Guid connectionId);
    Task<FakturoidConnection> CreateAsync(
        Guid userId,
        string accessToken,
        string refreshToken,
        DateTime expiresAt,
        string accountSlug,
        string? accountName = null);
    Task UpdateTokensAsync(Guid connectionId, string accessToken, string refreshToken, DateTime expiresAt);
    Task UpdateLastSyncedAsync(Guid connectionId);
    Task DisconnectAsync(Guid connectionId);
    Task<bool> IsConnectedAsync(Guid userId);
}

public class FakturoidConnectionProvider : IFakturoidConnectionProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public FakturoidConnectionProvider(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<FakturoidConnection?> GetByUserIdAsync(Guid userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.FakturoidConnections
            .FirstOrDefaultAsync(fc => fc.UserId == userId && fc.IsActive);
    }

    public async Task<FakturoidConnection?> GetByIdAsync(Guid connectionId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.FakturoidConnections
            .FirstOrDefaultAsync(fc => fc.Id == connectionId);
    }

    public async Task<FakturoidConnection> CreateAsync(
        Guid userId,
        string accessToken,
        string refreshToken,
        DateTime expiresAt,
        string accountSlug,
        string? accountName = null)
    {
        var connection = new FakturoidConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = expiresAt,
            AccountSlug = accountSlug,
            AccountName = accountName,
            ConnectedAt = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await using var context = _contextFactory.CreateDbContext();
        context.FakturoidConnections.Add(connection);
        await context.SaveChangesAsync();

        return connection;
    }

    public async Task UpdateTokensAsync(Guid connectionId, string accessToken, string refreshToken, DateTime expiresAt)
    {
        await using var context = _contextFactory.CreateDbContext();
        var connection = await context.FakturoidConnections.FindAsync(connectionId);
        if (connection != null)
        {
            connection.AccessToken = accessToken;
            connection.RefreshToken = refreshToken;
            connection.AccessTokenExpiresAt = expiresAt;
            connection.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateLastSyncedAsync(Guid connectionId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var connection = await context.FakturoidConnections.FindAsync(connectionId);
        if (connection != null)
        {
            connection.LastSyncedAt = DateTime.UtcNow;
            connection.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task DisconnectAsync(Guid connectionId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var connection = await context.FakturoidConnections.FindAsync(connectionId);
        if (connection != null)
        {
            connection.IsActive = false;
            connection.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsConnectedAsync(Guid userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.FakturoidConnections
            .AnyAsync(fc => fc.UserId == userId && fc.IsActive);
    }
}
