using Core;

namespace Frontend.Service.IService;

public interface ISessionService
{
    Task<List<Session>> GetMySessionsAsync(string userId);      // Hent alle sessioner for én user (creator eller deltager)
    Task<List<Session>> GetMyOpenSessionsAsync(string userId);  // nok ik nødvendigt heller
    Task<Session?> GetSessionBySessionIdAsync(string sessionId); // ved ikke om det nødvendigt
    Task<Session?> AddSessionAsync(string creatorUserId, Session session);
    Task<bool> UpdateSessionAsync(Session session);
    Task<bool> DeleteSessionAsync(string sessionId); // skal man kunne slette en session?
}