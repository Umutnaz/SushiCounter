using Core;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FriendsController : ControllerBase
{
    private readonly IMongoCollection<User> _users;
    private const string UsersCollection = "Users";

    public FriendsController()
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
                               ?? throw new InvalidOperationException("MONGO_CONNECTION_STRING is not set.");
        var databaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME")
                           ?? throw new InvalidOperationException("MONGO_DATABASE_NAME is not set.");

        var client = new MongoClient(connectionString);
        var db = client.GetDatabase(databaseName);
        _users = db.GetCollection<User>(UsersCollection);

        var nameIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Name),
            new CreateIndexOptions { Name = "ix_users_name" });
        _users.Indexes.CreateOne(nameIndex);
    }

    // GET: api/Friends/search?term=marie1  (uændret)
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<User>>> Search([FromQuery] string term)
    {
        term ??= string.Empty;
        var filter = Builders<User>.Filter.Regex(
            u => u.Name, new BsonRegularExpression(term, "i"));

        var list = await _users.Find(filter)
            .SortBy(u => u.Name)
            .Limit(50)
            .ToListAsync();

        return Ok(list);
    }

    // GET: api/Friends/{userId}/friends  -> læs direkte fra user
    [HttpGet("{userId}/friends")]
    public async Task<ActionResult<IEnumerable<User>>> GetFriends(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");

        var friends = await _users.Find(u => u.UserId == userId)
                                  .Project(u => u.Friends)
                                  .FirstOrDefaultAsync();

        return Ok(friends ?? new List<User>());
    }

    // DELETE: api/Friends/{userId}/{friendId}
    [HttpDelete("{userId}/{friendId}")]
    public async Task<IActionResult> RemoveFriend(string userId, string friendId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(friendId))
            return BadRequest("userId/friendId mangler.");

        // fjern venskab begge veje
        var pullFriend = Builders<User>.Update.PullFilter(u => u.Friends, f => f.UserId == friendId);
        await _users.UpdateOneAsync(u => u.UserId == userId, pullFriend);

        var pullFriend2 = Builders<User>.Update.PullFilter(u => u.Friends, f => f.UserId == userId);
        await _users.UpdateOneAsync(u => u.UserId == friendId, pullFriend2);

        // cleanup evt. pending mellem dem
        var pullIncoming = Builders<User>.Update.PullFilter(u => u.IncomingRequests,
            r => r.FromUserId == friendId && r.ToUserId == userId);
        await _users.UpdateOneAsync(u => u.UserId == userId, pullIncoming);

        var pullOutgoing = Builders<User>.Update.PullFilter(u => u.OutgoingRequests,
            r => r.FromUserId == userId && r.ToUserId == friendId);
        await _users.UpdateOneAsync(u => u.UserId == userId, pullOutgoing);

        var pullIncoming2 = Builders<User>.Update.PullFilter(u => u.IncomingRequests,
            r => r.FromUserId == userId && r.ToUserId == friendId);
        await _users.UpdateOneAsync(u => u.UserId == friendId, pullIncoming2);

        var pullOutgoing2 = Builders<User>.Update.PullFilter(u => u.OutgoingRequests,
            r => r.FromUserId == friendId && r.ToUserId == userId);
        await _users.UpdateOneAsync(u => u.UserId == friendId, pullOutgoing2);

        return NoContent();
    }

    public record SendRequestDto(string FromUserId, string ToUserId);

    // POST: api/Friends/request
    [HttpPost("request")]
    public async Task<ActionResult<FriendRequest>> SendRequest([FromBody] SendRequestDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.FromUserId) || string.IsNullOrWhiteSpace(dto.ToUserId))
            return BadRequest("FromUserId/ToUserId mangler.");
        if (dto.FromUserId == dto.ToUserId) return BadRequest("Kan ikke ansøge dig selv.");

        var fromUser = await _users.Find(u => u.UserId == dto.FromUserId).FirstOrDefaultAsync();
        var toUser   = await _users.Find(u => u.UserId == dto.ToUserId).FirstOrDefaultAsync();
        if (fromUser is null || toUser is null) return NotFound("En eller begge brugere findes ikke.");

        // FIX: gamle docs kan mangle felterne -> null
        fromUser.Friends ??= new();
        fromUser.OutgoingRequests ??= new();
        toUser.IncomingRequests ??= new();

        if (fromUser.Friends.Any(f => f.UserId == dto.ToUserId))
            return Conflict("I er allerede venner.");

        if (fromUser.OutgoingRequests.Any(r => r.ToUserId == dto.ToUserId) ||
            toUser.IncomingRequests.Any(r => r.FromUserId == dto.FromUserId))
            return Conflict("Der er allerede en afventende anmodning.");

        var req = new FriendRequest
        {
            FromUserId = dto.FromUserId,
            ToUserId   = dto.ToUserId,
            Status     = FriendRequestStatus.Pending,
            CreatedAt  = DateTime.UtcNow
        };

        await _users.UpdateOneAsync(u => u.UserId == dto.FromUserId,
            Builders<User>.Update.AddToSet(u => u.OutgoingRequests, req));

        await _users.UpdateOneAsync(u => u.UserId == dto.ToUserId,
            Builders<User>.Update.AddToSet(u => u.IncomingRequests, req));

        return Ok(req);
    }


    public record RespondDto(string RequestId, bool Accept);

    // POST: api/Friends/respond
    [HttpPost("respond")]
    public async Task<IActionResult> Respond([FromBody] RespondDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.RequestId))
            return BadRequest("RequestId mangler.");

        // find recipient der har requesten i IncomingRequests
        var toUser = await _users.Find(u => u.IncomingRequests.Any(r => r.RequestId == dto.RequestId))
                                 .FirstOrDefaultAsync();
        if (toUser is null) return NotFound("Anmodning ikke fundet.");

        var req = toUser.IncomingRequests.First(r => r.RequestId == dto.RequestId);
        if (req.Status != FriendRequestStatus.Pending)
            return Conflict("Anmodningen er allerede håndteret.");

        if (dto.Accept)
        {
            // hent begge brugere
            var from = await _users.Find(u => u.UserId == req.FromUserId).FirstOrDefaultAsync();
            var to = await _users.Find(u => u.UserId == req.ToUserId).FirstOrDefaultAsync();
            if (from is null || to is null) return NotFound("En bruger blev ikke fundet.");

            var shallowFrom = new User { UserId = from.UserId, Name = from.Name, Email = from.Email };
            var shallowTo = new User { UserId = to.UserId, Name = to.Name, Email = to.Email };

            await _users.UpdateOneAsync(u => u.UserId == req.ToUserId,
                Builders<User>.Update.AddToSet(u => u.Friends, shallowFrom));

            await _users.UpdateOneAsync(u => u.UserId == req.FromUserId,
                Builders<User>.Update.AddToSet(u => u.Friends, shallowTo));
        }

        // uanset accept/afvis: fjern pending begge steder
        await _users.UpdateOneAsync(u => u.UserId == req.ToUserId,
            Builders<User>.Update.PullFilter(u => u.IncomingRequests, r => r.RequestId == dto.RequestId));

        await _users.UpdateOneAsync(u => u.UserId == req.FromUserId,
            Builders<User>.Update.PullFilter(u => u.OutgoingRequests, r => r.RequestId == dto.RequestId));

        return NoContent();
    }

    // GET: api/Friends/{userId}/incoming
    [HttpGet("{userId}/incoming")]
    public async Task<ActionResult<IEnumerable<FriendRequest>>> Incoming(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");

        var me = await _users.Find(u => u.UserId == userId)
                             .Project(u => u.IncomingRequests)
                             .FirstOrDefaultAsync();

        var reqs = (me ?? new List<FriendRequest>())
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        var fromIds = reqs.Select(r => r.FromUserId).Distinct().ToList();
        var fromUsers = await _users.Find(u => fromIds.Contains(u.UserId!))
            .Project(u => new User { UserId = u.UserId, Name = u.Name, Email = u.Email })
            .ToListAsync();

        var dict = fromUsers.ToDictionary(u => u.UserId!, u => u);
        foreach (var r in reqs)
            if (dict.TryGetValue(r.FromUserId, out var fu)) r.FromUser = fu;

        return Ok(reqs);
    }

    // GET: api/Friends/{userId}/outgoing
    [HttpGet("{userId}/outgoing")]
    public async Task<ActionResult<IEnumerable<FriendRequest>>> Outgoing(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");

        var me = await _users.Find(u => u.UserId == userId)
                             .Project(u => u.OutgoingRequests)
                             .FirstOrDefaultAsync();

        var reqs = (me ?? new List<FriendRequest>())
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        var toIds = reqs.Select(r => r.ToUserId).Distinct().ToList();
        var toUsers = await _users.Find(u => toIds.Contains(u.UserId!))
            .Project(u => new User { UserId = u.UserId, Name = u.Name, Email = u.Email })
            .ToListAsync();

        var dict = toUsers.ToDictionary(u => u.UserId!, u => u);
        foreach (var r in reqs)
            if (dict.TryGetValue(r.ToUserId, out var tu)) r.ToUser = tu;

        return Ok(reqs);
    }
}
