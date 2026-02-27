using Core;

namespace Backend.Repositories.IRepository;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync();
    // Find user by email (email is stored in lowercase in DB)
    Task<User?> FindByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task<bool> UpdateAsync(User user);
    Task<User?> GetByIdAsync(string userId);
    Task<bool> DeleteAsync(string userId);
}
