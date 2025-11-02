using Core;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")] // => api/Friends
public class FriendsController : ControllerBase
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<FriendRequest> _requests;

    // Flyt disse til appsettings + DI når du vil rydde op
    private const string ConnectionString =
        "mongodb+srv://sushi_app:SushiTest123@cluster0.zndfk.mongodb.net/?retryWrites=true&w=majority&appName=Cluster0";
    private const string DatabaseName = "SushiCounter";
    private const string UsersCollection = "Users";
    private const string RequestsCollection = "FriendRequests";

    public FriendsController()
    {
        var client = new MongoClient(ConnectionString);
        var db = client.GetDatabase(DatabaseName);
        _users = db.GetCollection<User>(UsersCollection);
        _requests = db.GetCollection<FriendRequest>(RequestsCollection);

        // Indeks til hurtig søgning på navn
        var nameIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Name),
            new CreateIndexOptions { Name = "ix_users_name" });
        _users.Indexes.CreateOne(nameIndex);

        // Indeks til venneanmodninger
        var reqIndex = new CreateIndexModel<FriendRequest>(
            Builders<FriendRequest>.IndexKeys
                .Ascending(r => r.FromUserId)
                .Ascending(r => r.ToUserId)
                .Ascending(r => r.Status),
            new CreateIndexOptions { Name = "ix_reqs_from_to_status" });
        _requests.Indexes.CreateOne(reqIndex);
    }

    // GET: api/Friends/search?term=marie1
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<User>>> Search([FromQuery] string term)
    {
        term ??= string.Empty;
        var filter = Builders<User>.Filter.Regex(
            u => u.Name, new BsonRegularExpression(term, "i")); // case-insensitive contains

        var list = await _users.Find(filter)
            .SortBy(u => u.Name)
            .Limit(50)
            .ToListAsync();

        return Ok(list);
    }

    // GET: api/Friends/{userId}/friends
    [HttpGet("{userId}/friends")]
    public async Task<ActionResult<IEnumerable<User>>> GetFriends(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");

        var accepted = await _requests.Find(r =>
                (r.FromUserId == userId || r.ToUserId == userId) &&
                r.Status == FriendRequestStatus.Accepted)
            .ToListAsync();

        var friendIds = accepted.Select(r => r.FromUserId == userId ? r.ToUserId : r.FromUserId)
                                .Distinct()
                                .ToList();

        if (friendIds.Count == 0) return Ok(new List<User>());

        var friends = await _users.Find(u => friendIds.Contains(u.UserId!))
                                  .SortBy(u => u.Name)
                                  .ToListAsync();
        return Ok(friends);
    }

    // DELETE: api/Friends/{userId}/{friendId}  -> fjerner venskab begge veje
    [HttpDelete("{userId}/{friendId}")]
    public async Task<IActionResult> RemoveFriend(string userId, string friendId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(friendId))
            return BadRequest("userId/friendId mangler.");

        // Slet evt. accepterede anmodninger mellem de to (begge retninger)
        var delFilter = Builders<FriendRequest>.Filter.Or(
            Builders<FriendRequest>.Filter.And(
                Builders<FriendRequest>.Filter.Eq(r => r.FromUserId, userId),
                Builders<FriendRequest>.Filter.Eq(r => r.ToUserId, friendId)
            ),
            Builders<FriendRequest>.Filter.And(
                Builders<FriendRequest>.Filter.Eq(r => r.FromUserId, friendId),
                Builders<FriendRequest>.Filter.Eq(r => r.ToUserId, userId)
            )
        );
        await _requests.DeleteManyAsync(delFilter);

        // (Valgfrit) pull fra embedded Friends-listen hvis du bruger den
        var pull = Builders<User>.Update.PullFilter(u => u.Friends, f => f.UserId == friendId);
        await _users.UpdateOneAsync(u => u.UserId == userId, pull);
        var pull2 = Builders<User>.Update.PullFilter(u => u.Friends, f => f.UserId == userId);
        await _users.UpdateOneAsync(u => u.UserId == friendId, pull2);

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

        // Valider brugere
        var both = await _users.Find(u => u.UserId == dto.FromUserId || u.UserId == dto.ToUserId)
                               .Project(u => u.UserId)
                               .ToListAsync();
        if (both.Count != 2) return NotFound("En eller begge brugere findes ikke.");

        // Find eksisterende relation/anmodning i begge retninger
        var existing = await _requests.Find(r =>
                (r.FromUserId == dto.FromUserId && r.ToUserId == dto.ToUserId) ||
                (r.FromUserId == dto.ToUserId && r.ToUserId == dto.FromUserId))
            .SortByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing is { Status: FriendRequestStatus.Accepted })
            return Conflict("I er allerede venner.");

        if (existing is { Status: FriendRequestStatus.Pending })
            return Conflict("Der er allerede en afventende anmodning.");

        var req = new FriendRequest
        {
            FromUserId = dto.FromUserId,
            ToUserId = dto.ToUserId,
            Status = FriendRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _requests.InsertOneAsync(req);
        return Ok(req);
    }

    public record RespondDto(string RequestId, bool Accept);

    // POST: api/Friends/respond
    [HttpPost("respond")]
    public async Task<IActionResult> Respond([FromBody] RespondDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.RequestId))
            return BadRequest("RequestId mangler.");

        var req = await _requests.Find(r => r.RequestId == dto.RequestId).FirstOrDefaultAsync();
        if (req is null) return NotFound("Anmodning ikke fundet.");
        if (req.Status != FriendRequestStatus.Pending) return Conflict("Anmodningen er allerede håndteret.");

        if (dto.Accept)
        {
            // Acceptér
            var upd = Builders<FriendRequest>.Update
                .Set(r => r.Status, FriendRequestStatus.Accepted)
                .Set(r => r.RespondedAt, DateTime.UtcNow);
            await _requests.UpdateOneAsync(r => r.RequestId == req.RequestId, upd);

            // Tilføj til hinandens embedded Friends-lister (shallow kopier)
            var fromUser = await _users.Find(u => u.UserId == req.FromUserId).FirstOrDefaultAsync();
            var toUser = await _users.Find(u => u.UserId == req.ToUserId).FirstOrDefaultAsync();
            if (fromUser is null || toUser is null) return NotFound("En bruger blev ikke fundet.");

            var shallowFrom = new User { UserId = fromUser.UserId, Name = fromUser.Name, Email = fromUser.Email };
            var shallowTo = new User { UserId = toUser.UserId, Name = toUser.Name, Email = toUser.Email };

            var addTo = Builders<User>.Update.AddToSet(u => u.Friends, shallowFrom);
            await _users.UpdateOneAsync(u => u.UserId == req.ToUserId, addTo);

            var addFrom = Builders<User>.Update.AddToSet(u => u.Friends, shallowTo);
            await _users.UpdateOneAsync(u => u.UserId == req.FromUserId, addFrom);
        }
        else
        {
            // Afvis
            var upd = Builders<FriendRequest>.Update
                .Set(r => r.Status, FriendRequestStatus.Rejected)
                .Set(r => r.RespondedAt, DateTime.UtcNow);
            await _requests.UpdateOneAsync(r => r.RequestId == req.RequestId, upd);
        }

        return NoContent();
    }

    // GET: api/Friends/{userId}/incoming
    [HttpGet("{userId}/incoming")]
    public async Task<ActionResult<IEnumerable<FriendRequest>>> Incoming(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");

        var reqs = await _requests.Find(r => r.ToUserId == userId && r.Status == FriendRequestStatus.Pending)
                                  .SortByDescending(r => r.CreatedAt)
                                  .ToListAsync();

        // Hydrate FromUser (til nem visning)
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

        var reqs = await _requests.Find(r => r.FromUserId == userId && r.Status == FriendRequestStatus.Pending)
                                  .SortByDescending(r => r.CreatedAt)
                                  .ToListAsync();

        // Hydrate ToUser (valgfrit)
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
