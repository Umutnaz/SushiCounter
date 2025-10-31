using Core;

namespace Frontend.Service.IService;

public interface IParticipantService
{
    Task<bool> AddParticipantAsync(string sessionId, Participant p);   // add/update deltager
    Task<bool> RemoveParticipantAsync(string sessionId, string userId);   // slet deltager
}