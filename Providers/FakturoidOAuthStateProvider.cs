using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IFakturoidOAuthStateProvider
{
    Task StoreAsync(Guid userId, string stateHash, DateTime expiresAt);
    Task<bool> ValidateAndConsumeAsync(Guid userId, string stateHash);
}

public class FakturoidOAuthStateProvider : IFakturoidOAuthStateProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public FakturoidOAuthStateProvider(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task StoreAsync(Guid userId, string stateHash, DateTime expiresAt)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existingStates = await context.FakturoidOAuthStates
            .Where(state => state.UserId == userId)
            .ToListAsync();

        if (existingStates.Count > 0)
        {
            context.FakturoidOAuthStates.RemoveRange(existingStates);
        }

        var state = new FakturoidOAuthState
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StateHash = stateHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        context.FakturoidOAuthStates.Add(state);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ValidateAndConsumeAsync(Guid userId, string stateHash)
    {
        await using var context = _contextFactory.CreateDbContext();
        var state = await context.FakturoidOAuthStates
            .FirstOrDefaultAsync(item => item.UserId == userId && item.StateHash == stateHash);

        if (state == null)
        {
            return false;
        }

        if (state.ExpiresAt <= DateTime.UtcNow)
        {
            context.FakturoidOAuthStates.Remove(state);
            await context.SaveChangesAsync();
            return false;
        }

        context.FakturoidOAuthStates.Remove(state);
        await context.SaveChangesAsync();
        return true;
    }
}
