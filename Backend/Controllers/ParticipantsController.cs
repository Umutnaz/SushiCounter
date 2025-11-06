using Core;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Backend.Controllers;

[ApiController]
[Route("api/Sessions/{sessionId}/participants")]
public class ParticipantsController : ControllerBase
{
    private readonly IMongoCollection<Session> _sessions;

    private const string SessionsCollection = "Sessions";

    public ParticipantsController()
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
                               ?? throw new InvalidOperationException("MONGO_CONNECTION_STRING is not set.");
        var databaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME")
                           ?? throw new InvalidOperationException("MONGO_DATABASE_NAME is not set.");

        var client = new MongoClient(connectionString);
        var db = client.GetDatabase(databaseName);
        _sessions = db.GetCollection<Session>(SessionsCollection);
    }

    // DELETE: api/Sessions/{sessionId}/participants/{userId}
    // Fjerner en deltager og opdaterer total count og rating
    [HttpDelete("{userId}")]
    public async Task<IActionResult> Remove(string sessionId, string userId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userId))
            return BadRequest("sessionId/userId mangler.");

        var s = await _sessions.Find(x => x.SessionId == sessionId).FirstOrDefaultAsync();
        if (s is null) return NotFound("Session ikke fundet.");

        // Fjern deltager
        s.Participants.RemoveAll(p => p.UserId == userId);

        // Reberegn totals og rating
        s.TotalCount = s.Participants.Sum(pp => Math.Max(0, pp.Count));
        var ratings = s.Participants
            .Where(pp => pp.Rating.HasValue)
            .Select(pp => pp.Rating!.Value)
            .ToList();

        s.Rating = ratings.Count == 0
            ? (int?)null
            : Math.Clamp((int)Math.Round(ratings.Average(), MidpointRounding.AwayFromZero), 1, 10);

        await _sessions.ReplaceOneAsync(x => x.SessionId == sessionId, s);
        return NoContent();
    }

    // PUT: api/Sessions/{sessionId}/participants
    // Tilføjer eller opdaterer en deltager, plusser counts og opdaterer rating og total
    [HttpPut]
    public async Task<IActionResult> AddOrUpdate(string sessionId, [FromBody] Participant p)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId mangler.");
        if (p is null || string.IsNullOrWhiteSpace(p.UserId)) return BadRequest("Participant mangler.");

        var s = await _sessions.Find(x => x.SessionId == sessionId).FirstOrDefaultAsync();
        if (s is null) return NotFound("Session ikke fundet.");
        if (!s.IsActive) return Conflict("Session er lukket.");

        var existing = s.Participants.FirstOrDefault(x => x.UserId == p.UserId);
        if (existing is null)
        {
            s.Participants.Add(new Participant
            {
                UserId = p.UserId,
                Count = Math.Max(0, p.Count),
                Rating = p.Rating is null ? null : Math.Clamp(p.Rating.Value, 1, 10)
            });
        }
        else
        {
            existing.Count = Math.Max(0, existing.Count + p.Count);
            existing.Rating = p.Rating is null ? null : Math.Clamp(p.Rating.Value, 1, 10);
        }

        // Reberegn totals og gennemsnitlig rating (kun tal fra 1-10)
        s.TotalCount = s.Participants.Sum(pp => Math.Max(0, pp.Count));
        var ratings = s.Participants
            .Where(pp => pp.Rating.HasValue)
            .Select(pp => pp.Rating!.Value)
            .ToList();

        s.Rating = ratings.Count == 0
            ? (int?)null
            : Math.Clamp((int)Math.Round(ratings.Average(), MidpointRounding.AwayFromZero), 1, 10);

        await _sessions.ReplaceOneAsync(x => x.SessionId == sessionId, s);
        return NoContent();
    }
}
