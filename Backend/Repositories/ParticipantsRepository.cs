using Core;
using MongoDB.Driver;
using Backend.Repositories.IRepository;

namespace Backend.Repositories;

public class ParticipantsRepository : IParticipantsRepository
{
    private readonly IMongoCollection<Session> _sessions;

    public ParticipantsRepository(IMongoClient client)
    {
        var databaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME")
                           ?? throw new InvalidOperationException("MONGO_DATABASE_NAME is not set.");
        var db = client.GetDatabase(databaseName);
        _sessions = db.GetCollection<Session>("Sessions");
    }

    public async Task<bool> RemoveParticipantAsync(string sessionId, string userId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userId)) return false;

        var s = await _sessions.Find(x => x.SessionId == sessionId).FirstOrDefaultAsync();
        if (s is null) return false;

        s.Participants.RemoveAll(p => p.UserId == userId);
        s.TotalCount = s.Participants.Sum(pp => Math.Max(0, pp.Count));

        var participantsCount = s.Participants.Count;
        if (participantsCount == 0) s.Rating = null;
        else
        {
            var sumRatings = s.Participants.Select(pp => Math.Clamp(pp.Rating ?? 0, 0, 10)).Sum();
            var avg = sumRatings / (double)participantsCount;
            s.Rating = Math.Clamp((int)Math.Round(avg, MidpointRounding.AwayFromZero), 0, 10);
        }

        await _sessions.ReplaceOneAsync(x => x.SessionId == sessionId, s);
        return true;
    }

    public async Task<bool> AddOrUpdateParticipantAsync(string sessionId, Participant p)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || p is null || string.IsNullOrWhiteSpace(p.UserId)) return false;

        var s = await _sessions.Find(x => x.SessionId == sessionId).FirstOrDefaultAsync();
        if (s is null) return false;
        if (!s.IsActive) return false;

        var existing = s.Participants.FirstOrDefault(x => x.UserId == p.UserId);
        if (existing is null)
        {
            s.Participants.Add(new Participant { UserId = p.UserId, Count = Math.Max(0, p.Count), Rating = Math.Clamp(p.Rating ?? 0, 0, 10) });
        }
        else
        {
            existing.Count = Math.Max(0, existing.Count + p.Count);
            if (p.Rating.HasValue) existing.Rating = Math.Clamp(p.Rating.Value, 0, 10);
        }

        s.TotalCount = s.Participants.Sum(pp => Math.Max(0, pp.Count));

        var participantsCount = s.Participants.Count;
        if (participantsCount == 0) s.Rating = null;
        else
        {
            var sumRatings = s.Participants.Select(pp => Math.Clamp(pp.Rating ?? 0, 0, 10)).Sum();
            var avg = sumRatings / (double)participantsCount;
            s.Rating = Math.Clamp((int)Math.Round(avg, MidpointRounding.AwayFromZero), 0, 10);
        }

        await _sessions.ReplaceOneAsync(x => x.SessionId == sessionId, s);
        return true;
    }
}

