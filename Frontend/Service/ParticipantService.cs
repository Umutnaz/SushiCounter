using Core;
using Frontend.Service.IService;
using System.Net.Http;
using System.Net.Http.Json;

namespace Frontend.Services
{
    public class ParticipantService : IParticipantService
    {
        private readonly HttpClient _httpClient;
        // Deltagere ligger under sessions, så vi bruger nested routes:
        private const string BaseSessionsUrl = "http://localhost:5116/api/Sessions";

        public ParticipantService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> AddParticipantAsync(string sessionId, Participant p)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || p is null || string.IsNullOrWhiteSpace(p.UserId))
                return false;

            var response = await _httpClient.PutAsJsonAsync($"{BaseSessionsUrl}/{sessionId}/participants", p);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> RemoveParticipantAsync(string sessionId, string userId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userId))
                return false;

            var response = await _httpClient.DeleteAsync($"{BaseSessionsUrl}/{sessionId}/participants/{userId}");
            return response.IsSuccessStatusCode;
        }
    }
}