using Core;
using MongoDB.Driver;
using Backend.Repositories.IRepository;

namespace Backend.Repositories;

// UserRepository: All DB operations for User documents.
// - Create, read, update, delete users
// - Login is implemented via FindByEmailAsync: controller is responsible for hashing/verifying the password input
// - Keep repository focused on DB work; controller handles validation and hashing
public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    public UserRepository(IMongoClient client)
    {
        var databaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME")
                           ?? throw new InvalidOperationException("MONGO_DATABASE_NAME is not set.");
        var db = client.GetDatabase(databaseName);
        _users = db.GetCollection<User>("Users");

        var indexKeys = Builders<User>.IndexKeys;
        var emailIndex = new CreateIndexModel<User>(indexKeys.Ascending(u => u.Email), new CreateIndexOptions { Unique = true, Name = "ux_users_email" });
        var nameIndex = new CreateIndexModel<User>(indexKeys.Ascending(u => u.Name), new CreateIndexOptions { Unique = true, Name = "ux_users_name" });
        _users.Indexes.CreateMany(new[] { emailIndex, nameIndex });
    }

    public async Task<List<User>> GetAllAsync() => await _users.Find(_ => true).ToListAsync();

    public async Task<User?> FindByEmailAsync(string email)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        return await _users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync();
    }

    public async Task<User> CreateAsync(User user)
    {
        await _users.InsertOneAsync(user);
        return user;
    }

    public async Task<bool> UpdateAsync(User user)
    {
        var filter = Builders<User>.Filter.Eq(u => u.UserId, user.UserId);
        var update = Builders<User>.Update
            .Set(u => u.Name, user.Name)
            .Set(u => u.Email, user.Email)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(user.Password))
        {
            // assume already hashed by controller
            update = update.Set(u => u.Password, user.Password);
        }

        var res = await _users.UpdateOneAsync(filter, update);
        return res.MatchedCount > 0;
    }

    public async Task<User?> GetByIdAsync(string userId)
    {
        return await _users.Find(u => u.UserId == userId).FirstOrDefaultAsync();
    }

    public async Task<bool> DeleteAsync(string userId)
    {
        var res = await _users.DeleteOneAsync(u => u.UserId == userId);
        return res.DeletedCount > 0;
    }
}
