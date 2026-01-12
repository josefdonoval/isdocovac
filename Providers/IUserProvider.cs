using Isdocovac.Models;

namespace Isdocovac.Providers;

public interface IUserProvider
{
    Task<User> CreateUserAsync(string username, string email, string passwordHash, string passwordSalt);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User?> GetUserByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task UpdateUserAsync(User user);
    Task DeleteUserAsync(Guid userId);
}
