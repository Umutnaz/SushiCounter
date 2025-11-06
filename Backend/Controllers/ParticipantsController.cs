using Core;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Backend.Controllers;

[ApiController]
[Route("api/Sessions/{sessionId}/participants")]
public class ParticipantsController : ControllerBase
{
    private readonly IMongoCollection<Session> _sessions;

    // Flyt til appsettings + DI når du rydder op

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
    // Fjerner en deltager (creator kan fjerne andre; deltager kan fjerne sig selv – auth/policy håndhæves typisk via middleware)
    [HttpDelete("{userId}")]
    public async Task<IActionResult> Remove(string sessionId, string userId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userId))
            return BadRequest("sessionId/userId mangler.");

        var update = Builders<Session>.Update.PullFilter(s => s.Participants, p => p.UserId == userId);
        var result = await _sessions.UpdateOneAsync(s => s.SessionId == sessionId, update);

        if (result.MatchedCount == 0) return NotFound("Session ikke fundet.");
        // Hvis Pull ikke fandt deltageren, ændres ModifiedCount=0 – det er ok at returnere NoContent alligevel.
        return NoContent();
    }
    // ParticipantsController.cs – funktionen der modtager count og upserter deltageren
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
            existing.Count = Math.Max(0, p.Count);
            existing.Rating = p.Rating is null ? null : Math.Clamp(p.Rating.Value, 1, 10);
        }

        await _sessions.ReplaceOneAsync(x => x.SessionId == sessionId, s);
        return NoContent();
    }

}
