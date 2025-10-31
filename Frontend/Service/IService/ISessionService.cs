using Core;

namespace Frontend.Service.IService;

public interface ISessionService
{
    Task<List<Session>> GetAllSessionsAsync();                   // Hent alle sessions (fra alle users)
    Task<Session?> GetSessionBySessionIdAsync(string sessionId); // Hent én session
    Task<Session?> AddSessionAsync(string creatorUserId, Session session); // Opret session under user
    Task<bool> UpdateSessionAsync(Session session);              // Opdater en embedded session (kræver SessionId)
    Task<bool> DeleteSessionAsync(string sessionId);             // Slet en embedded session via id
}