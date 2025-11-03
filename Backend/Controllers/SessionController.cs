using Core;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")] // => api/Sessions
public class SessionsController : ControllerBase
{
    private readonly IMongoCollection<Session> _sessions;

    // Flyt til appsettings + DI når du rydder op
    private const string SessionsCollection = "Sessions";

    public SessionsController()
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
                               ?? throw new InvalidOperationException("MONGO_CONNECTION_STRING is not set.");
        var databaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME")
                           ?? throw new InvalidOperationException("MONGO_DATABASE_NAME is not set.");

        var client = new MongoClient(connectionString);
        var db = client.GetDatabase(databaseName);
        _sessions = db.GetCollection<Session>(SessionsCollection);

        // Indeks: søg på creator, deltagere og status
        var idx1 = new CreateIndexModel<Session>(
            Builders<Session>.IndexKeys.Ascending(s => s.CreatorId),
            new CreateIndexOptions { Name = "ix_sessions_creator" });

        var idx2 = new CreateIndexModel<Session>(
            Builders<Session>.IndexKeys.Ascending("Participants.UserId"),
            new CreateIndexOptions { Name = "ix_sessions_participants_userid" });

        var idx3 = new CreateIndexModel<Session>(
            Builders<Session>.IndexKeys.Ascending(s => s.IsActive),
            new CreateIndexOptions { Name = "ix_sessions_isactive" });

        _sessions.Indexes.CreateMany(new[] { idx1, idx2, idx3 });
    }

    // GET: api/Sessions/mine/{userId}
    // Henter ALLE sessioner hvor brugeren er creator eller deltager
    [HttpGet("mine/{userId}")]
    public async Task<ActionResult<IEnumerable<Session>>> GetMine(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");

        var filter = Builders<Session>.Filter.Or(
            Builders<Session>.Filter.Eq(s => s.CreatorId, userId),
            Builders<Session>.Filter.ElemMatch(s => s.Participants, p => p.UserId == userId)
        );

        var list = await _sessions.Find(filter)
                                  .SortByDescending(s => s.CreatedAt)
                                  .ToListAsync();

        return Ok(list);
    }

    // GET: api/Sessions/open/{userId}
    // Åbne sessioner for en given bruger (til SushiCounter "vælg session")
    [HttpGet("open/{userId}")]
    public async Task<ActionResult<IEnumerable<Session>>> GetOpenForUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");

        var filter = Builders<Session>.Filter.And(
            Builders<Session>.Filter.Eq(s => s.IsActive, true),
            Builders<Session>.Filter.Or(
                Builders<Session>.Filter.Eq(s => s.CreatorId, userId),
                Builders<Session>.Filter.ElemMatch(s => s.Participants, p => p.UserId == userId)
            )
        );

        var list = await _sessions.Find(filter)
                                  .SortByDescending(s => s.CreatedAt)
                                  .ToListAsync();

        return Ok(list);
    }

    // GET: api/Sessions/{sessionId}
    [HttpGet("{sessionId}")]
    public async Task<ActionResult<Session>> GetById(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId mangler.");

        var s = await _sessions.Find(x => x.SessionId == sessionId).FirstOrDefaultAsync();
        if (s is null) return NotFound();

        // (Valgfrit) beregn afledte felter on-read, hvis du vil sende dem “friske”
        s.TotalCount = s.Participants?.Sum(p => p.Count) ?? 0;
        var ratings = s.Participants?.Where(p => p.Rating.HasValue).Select(p => p.Rating!.Value).ToList() ?? new();
        s.Rating = ratings.Count == 0 ? null : (int?)Math.Round(ratings.Average());

        return Ok(s);
    }

    // POST: api/Sessions/opret/{creatorUserId}
    [HttpPost("opret/{creatorUserId}")]
    public async Task<ActionResult<Session>> Create(string creatorUserId, [FromBody] Session input)
    {
        if (string.IsNullOrWhiteSpace(creatorUserId)) return BadRequest("creatorUserId mangler.");
        if (input is null) return BadRequest("Body mangler.");
        if (string.IsNullOrWhiteSpace(input.Title)) return BadRequest("Title er påkrævet.");

        var newSession = new Session
        {
            Title = input.Title.Trim(),
            RestaurantName = input.RestaurantName?.Trim(),
            Description = input.Description?.Trim(),
            CreatorId = creatorUserId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Participants = input.Participants ?? new List<Participant>(),
            TotalCount = 0, // beregnes/returneres på read
            Rating = null
        };

        await _sessions.InsertOneAsync(newSession);
        return CreatedAtAction(nameof(GetById), new { sessionId = newSession.SessionId }, newSession);
    }

    // PUT: api/Sessions/update
    // Opdaterer meta + status; ændringer på participants sker i ParticipantsController
    [HttpPut("update")]
    public async Task<IActionResult> Update([FromBody] Session updated)
    {
        if (updated is null || string.IsNullOrWhiteSpace(updated.SessionId))
            return BadRequest("SessionId mangler.");

        var update = Builders<Session>.Update
            .Set(s => s.Title, updated.Title)
            .Set(s => s.RestaurantName, updated.RestaurantName)
            .Set(s => s.Description, updated.Description)
            .Set(s => s.IsActive, updated.IsActive);

        var result = await _sessions.UpdateOneAsync(s => s.SessionId == updated.SessionId, update);
        if (result.MatchedCount == 0) return NotFound();

        return NoContent();
    }

    // DELETE: api/Sessions/{sessionId}
    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> Delete(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId mangler.");

        var result = await _sessions.DeleteOneAsync(s => s.SessionId == sessionId);
        if (result.DeletedCount == 0) return NotFound();

        return NoContent();
    }
}
