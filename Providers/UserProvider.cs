using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IUserProvider
{
    Task<User> CreateUserAsync(string email, string? username = null, string? displayName = null);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User> GetOrCreateByEmailAsync(string email, string? username = null, string? displayName = null);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task UpdateUserAsync(User user);
    Task UpdateEmailVerificationAsync(Guid userId, bool verified);
    Task UpdateLastLoginAsync(Guid userId);
    Task DeleteUserAsync(Guid userId);
}

public class UserProvider : IUserProvider
{
    private readonly ApplicationDbContext _context;

    public UserProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User> CreateUserAsync(string email, string? username = null, string? displayName = null)
    {
        // Generate username from email if not provided
        var finalUsername = username ?? email.Split('@')[0];

        // Ensure username is unique
        var existingUser = await GetUserByUsernameAsync(finalUsername);
        if (existingUser != null)
        {
            // Append random suffix if username already exists
            finalUsername = $"{finalUsername}_{Guid.NewGuid().ToString("N")[..6]}";
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = finalUsername,
            Email = email,
            DisplayName = displayName ?? finalUsername,
            EmailVerified = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> GetOrCreateByEmailAsync(string email, string? username = null, string? displayName = null)
    {
        var user = await GetUserByEmailAsync(email);
        if (user == null)
        {
            user = await CreateUserAsync(email, username, displayName);
        }
        return user;
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _context.Users
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task UpdateUserAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateEmailVerificationAsync(Guid userId, bool verified)
    {
        var user = await GetUserByIdAsync(userId);
        if (user != null)
        {
            user.EmailVerified = verified;
            user.EmailVerifiedAt = verified ? DateTime.UtcNow : null;
            await UpdateUserAsync(user);
        }
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        var user = await GetUserByIdAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await UpdateUserAsync(user);
        }
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }
}
