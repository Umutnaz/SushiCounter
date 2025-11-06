using Core;

namespace Frontend.Service.IService;

public interface IParticipantService
{
    Task<bool> AddParticipantAsync(string sessionId, Participant p);     // add/update deltager (eg. sæt count/rating)
    Task<bool> RemoveParticipantAsync(string sessionId, string userId);  // fjern deltager

    Task<int> AddCountToSessionAsync(int currentCount);
    Task<bool> CommitLocalCountToSessionAsync(string sessionId, string userId, int? rating = null);
    Task <int> GetCurrentLocalCountAsync();
}