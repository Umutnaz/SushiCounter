using Core;
using System.Net.Http;
using System.Net.Http.Json;
using Frontend.Service.IService;

namespace Frontend.Services
{
    public class SessionService : ISessionService
    {
        private readonly HttpClient _httpClient;
        private const string BaseURL = "api/Sessions";

        public SessionService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<Session>> GetMySessionsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return new List<Session>();
            var sessions = await _httpClient.GetFromJsonAsync<List<Session>>($"{BaseURL}/mine/{userId}");
            return sessions ?? new List<Session>();
        }

        public async Task<List<Session>> GetMyOpenSessionsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return new List<Session>();
            var sessions = await _httpClient.GetFromJsonAsync<List<Session>>($"{BaseURL}/open/{userId}");
            return sessions ?? new List<Session>();
        }

        public async Task<Session?> GetSessionBySessionIdAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return null;

            return await _httpClient.GetFromJsonAsync<Session>($"{BaseURL}/{sessionId}");
        }

        public async Task<Session?> AddSessionAsync(string creatorUserId, Session s)
        {
            if (string.IsNullOrWhiteSpace(creatorUserId) || s is null)
                throw new ArgumentException("creatorUserId og session er påkrævet.");

            var response = await _httpClient.PostAsJsonAsync($"{BaseURL}/opret/{creatorUserId}", s);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var code = (int)response.StatusCode;
                
                throw new HttpRequestException(
                    string.IsNullOrWhiteSpace(body)
                        ? $"Oprettelse fejlede ({code})."
                        : body);
            }
            return await response.Content.ReadFromJsonAsync<Session>();
        }


        public async Task<bool> UpdateSessionAsync(Session s)
        {
            if (s is null || string.IsNullOrWhiteSpace(s.SessionId))
                return false;

            var response = await _httpClient.PutAsJsonAsync($"{BaseURL}/update", s);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteSessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return false;

            var response = await _httpClient.DeleteAsync($"{BaseURL}/{sessionId}");
            return response.IsSuccessStatusCode;
        }
    }
}
