using Core;
using MongoDB.Bson;
using MongoDB.Driver;
using Backend.Repositories.IRepository;

namespace Backend.Repositories;

// FriendsRepository: All DB operations related to users' friends and friend-requests.
// - Search users, list friends, create/remove friend-requests, accept/reject requests
// - Keeps controller logic small: controllers validate inputs and map to HTTP responses, repository does DB work
public class FriendsRepository : IFriendsRepository
{
    private readonly IMongoCollection<User> _users;
    public FriendsRepository(IMongoClient client)
    {
        var databaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME")
                           ?? throw new InvalidOperationException("MONGO_DATABASE_NAME is not set.");
        var db = client.GetDatabase(databaseName);
        _users = db.GetCollection<User>("Users");

        var nameIndex = new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.Name), new CreateIndexOptions { Name = "ix_users_name" });
        _users.Indexes.CreateOne(nameIndex);
    }

    public async Task<List<User>> SearchAsync(string? term)
    {
        term ??= string.Empty;
        var filter = Builders<User>.Filter.Regex(u => u.Name, new MongoDB.Bson.BsonRegularExpression(term, "i"));
        var list = await _users.Find(filter).SortBy(u => u.Name).Limit(50).ToListAsync();
        return list;
    }

    public async Task<List<User>> GetFriendsAsync(string userId)
    {
        var friends = await _users.Find(u => u.UserId == userId).Project(u => u.Friends).FirstOrDefaultAsync();
        return friends ?? new List<User>();
    }

    public async Task<bool> RemoveFriendAsync(string userId, string friendId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(friendId)) return false;

        var pullFriend = Builders<User>.Update.PullFilter(u => u.Friends, f => f.UserId == friendId);
        await _users.UpdateOneAsync(u => u.UserId == userId, pullFriend);

        var pullFriend2 = Builders<User>.Update.PullFilter(u => u.Friends, f => f.UserId == userId);
        await _users.UpdateOneAsync(u => u.UserId == friendId, pullFriend2);

        var pullIncoming = Builders<User>.Update.PullFilter(u => u.IncomingRequests, r => r.FromUserId == friendId && r.ToUserId == userId);
        await _users.UpdateOneAsync(u => u.UserId == userId, pullIncoming);

        var pullOutgoing = Builders<User>.Update.PullFilter(u => u.OutgoingRequests, r => r.FromUserId == userId && r.ToUserId == friendId);
        await _users.UpdateOneAsync(u => u.UserId == userId, pullOutgoing);

        var pullIncoming2 = Builders<User>.Update.PullFilter(u => u.IncomingRequests, r => r.FromUserId == userId && r.ToUserId == friendId);
        await _users.UpdateOneAsync(u => u.UserId == friendId, pullIncoming2);

        var pullOutgoing2 = Builders<User>.Update.PullFilter(u => u.OutgoingRequests, r => r.FromUserId == friendId && r.ToUserId == userId);
        await _users.UpdateOneAsync(u => u.UserId == friendId, pullOutgoing2);

        return true;
    }

    public async Task<FriendRequest?> SendRequestAsync(string fromUserId, string toUserId)
    {
        if (string.IsNullOrWhiteSpace(fromUserId) || string.IsNullOrWhiteSpace(toUserId)) return null;
        if (fromUserId == toUserId) return null;

        var fromUser = await _users.Find(u => u.UserId == fromUserId).FirstOrDefaultAsync();
        var toUser = await _users.Find(u => u.UserId == toUserId).FirstOrDefaultAsync();
        if (fromUser is null || toUser is null) return null;

        fromUser.Friends ??= new();
        fromUser.OutgoingRequests ??= new();
        toUser.IncomingRequests ??= new();

        if (fromUser.Friends.Any(f => f.UserId == toUserId)) return null; // already friends
        if (fromUser.OutgoingRequests.Any(r => r.ToUserId == toUserId) || toUser.IncomingRequests.Any(r => r.FromUserId == fromUserId)) return null;

        var req = new FriendRequest { FromUserId = fromUserId, ToUserId = toUserId, Status = FriendRequestStatus.Pending, CreatedAt = DateTime.UtcNow };

        await _users.UpdateOneAsync(u => u.UserId == fromUserId, Builders<User>.Update.AddToSet(u => u.OutgoingRequests, req));
        await _users.UpdateOneAsync(u => u.UserId == toUserId, Builders<User>.Update.AddToSet(u => u.IncomingRequests, req));

        return req;
    }

    public async Task<bool> RespondAsync(string requestId, bool accept)
    {
        if (string.IsNullOrWhiteSpace(requestId)) return false;

        var toUser = await _users.Find(u => u.IncomingRequests.Any(r => r.RequestId == requestId)).FirstOrDefaultAsync();
        if (toUser is null) return false;

        var req = toUser.IncomingRequests.First(r => r.RequestId == requestId);
        if (req.Status != FriendRequestStatus.Pending) return false;

        if (accept)
        {
            var from = await _users.Find(u => u.UserId == req.FromUserId).FirstOrDefaultAsync();
            var to = await _users.Find(u => u.UserId == req.ToUserId).FirstOrDefaultAsync();
            if (from is null || to is null) return false;

            var shallowFrom = new User { UserId = from.UserId, Name = from.Name, Email = from.Email };
            var shallowTo = new User { UserId = to.UserId, Name = to.Name, Email = to.Email };

            await _users.UpdateOneAsync(u => u.UserId == req.ToUserId, Builders<User>.Update.AddToSet(u => u.Friends, shallowFrom));
            await _users.UpdateOneAsync(u => u.UserId == req.FromUserId, Builders<User>.Update.AddToSet(u => u.Friends, shallowTo));
        }

        await _users.UpdateOneAsync(u => u.UserId == req.ToUserId, Builders<User>.Update.PullFilter(u => u.IncomingRequests, r => r.RequestId == requestId));
        await _users.UpdateOneAsync(u => u.UserId == req.FromUserId, Builders<User>.Update.PullFilter(u => u.OutgoingRequests, r => r.RequestId == requestId));

        return true;
    }

    public async Task<List<FriendRequest>> GetIncomingAsync(string userId)
    {
        var me = await _users.Find(u => u.UserId == userId).Project(u => u.IncomingRequests).FirstOrDefaultAsync();
        var reqs = (me ?? new List<FriendRequest>()).OrderByDescending(r => r.CreatedAt).ToList();

        var fromIds = reqs.Select(r => r.FromUserId).Distinct().ToList();
        var fromUsers = await _users.Find(u => fromIds.Contains(u.UserId!)).Project(u => new User { UserId = u.UserId, Name = u.Name, Email = u.Email }).ToListAsync();
        var dict = fromUsers.ToDictionary(u => u.UserId!, u => u);
        foreach (var r in reqs)
            if (dict.TryGetValue(r.FromUserId, out var fu)) r.FromUser = fu;

        return reqs;
    }

    public async Task<List<FriendRequest>> GetOutgoingAsync(string userId)
    {
        var me = await _users.Find(u => u.UserId == userId).Project(u => u.OutgoingRequests).FirstOrDefaultAsync();
        var reqs = (me ?? new List<FriendRequest>()).OrderByDescending(r => r.CreatedAt).ToList();

        var toIds = reqs.Select(r => r.ToUserId).Distinct().ToList();
        var toUsers = await _users.Find(u => toIds.Contains(u.UserId!)).Project(u => new User { UserId = u.UserId, Name = u.Name, Email = u.Email }).ToListAsync();
        var dict = toUsers.ToDictionary(u => u.UserId!, u => u);
        foreach (var r in reqs)
            if (dict.TryGetValue(r.ToUserId, out var tu)) r.ToUser = tu;

        return reqs;
    }
}
