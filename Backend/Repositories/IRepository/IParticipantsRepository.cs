using Core;

namespace Backend.Repositories.IRepository;

public interface IParticipantsRepository
{
    Task<bool> RemoveParticipantAsync(string sessionId, string userId);
    Task<bool> AddOrUpdateParticipantAsync(string sessionId, Participant p);
}

