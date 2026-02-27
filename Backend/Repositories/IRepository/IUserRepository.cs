using Core;

namespace Backend.Repositories.IRepository;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync();
    Task<User?> FindByEmailAndPasswordHashAsync(string email, string passwordHash);
    Task<User> CreateAsync(User user);
    Task<bool> UpdateAsync(User user);
    Task<User?> GetByIdAsync(string userId);
    Task<bool> DeleteAsync(string userId);
}

