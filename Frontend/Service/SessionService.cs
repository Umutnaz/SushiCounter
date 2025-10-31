using Core;
using System.Net.Http;
using System.Net.Http.Json;

namespace ComwellWeb.Services
{
    public class SessionService : Frontend.Service.IService.ISessionService
    {
        private readonly HttpClient _httpClient;
        private readonly string BaseURL = "http://localhost:5116/api/Sessions";

        public SessionService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Hent alle sessions (uanset ejer)
        public async Task<List<Session>> GetAllSessionsAsync()
        {
            var sessions = await _httpClient.GetFromJsonAsync<List<Session>>(BaseURL);
            return sessions ?? new List<Session>();
        }

        // Hent en bestemt session via dens SessionId
        public async Task<Session?> GetSessionBySessionIdAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return null;

            return await _httpClient.GetFromJsonAsync<Session>($"{BaseURL}/{sessionId}");
        }

        // Opret en session under en bestemt user (creatorUserId)
        // Forventer backend-route: POST /api/Sessions/opret/{creatorUserId}
        public async Task<Session?> AddSessionAsync(string creatorUserId, Session s)
        {
            if (string.IsNullOrWhiteSpace(creatorUserId) || s is null)
                return null;

            var response = await _httpClient.PostAsJsonAsync($"{BaseURL}/opret/{creatorUserId}", s);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<Session>();
        }

        // Opdater en session
        // Forventer backend-route: PUT /api/Sessions/update
        public async Task<bool> UpdateSessionAsync(Session s)
        {
            if (s is null || string.IsNullOrWhiteSpace(s.SessionId))
                return false;

            var response = await _httpClient.PutAsJsonAsync($"{BaseURL}/update", s);
            return response.IsSuccessStatusCode;
        }

        // Slet en session
        // Forventer backend-route: DELETE /api/Sessions/{sessionId}
        public async Task<bool> DeleteSessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return false;

            var response = await _httpClient.DeleteAsync($"{BaseURL}/{sessionId}");
            return response.IsSuccessStatusCode;
        }
    }
}
