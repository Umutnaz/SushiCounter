using Core;
using Frontend.Service.IService;
using System.Net.Http.Json;
using Blazored.LocalStorage;

namespace Frontend.Services
{
    public class ParticipantService : IParticipantService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private string BaseURL = "api/Users";

        public ParticipantService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        public async Task<bool> AddParticipantAsync(string sessionId, Participant p)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || p is null || string.IsNullOrWhiteSpace(p.UserId))
                return false;

            var response = await _httpClient.PutAsJsonAsync($"{BaseURL}/{sessionId}/participants", p);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> RemoveParticipantAsync(string sessionId, string userId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userId))
                return false;

            var response = await _httpClient.DeleteAsync($"{BaseURL}/{sessionId}/participants/{userId}");
            return response.IsSuccessStatusCode;
        }
        public async Task<int> AddCountToSessionAsync(int delta)
        {
            var key = "participantTotalCount";

            var total = 0;
            if (await _localStorage.ContainKeyAsync(key))
                total = await _localStorage.GetItemAsync<int>(key);

            total = checked(total + delta);
            await _localStorage.SetItemAsync(key, total);
            return total;
        }
        public async Task<int> GetCurrentLocalCountAsync()
        {
            var key = "participantTotalCount";
            if (await _localStorage.ContainKeyAsync(key))
                return await _localStorage.GetItemAsync<int>(key);

            return 0;
        }

        public async Task<bool> CommitLocalCountToSessionAsync(string sessionId, string userId, int? rating = null)
        {
            var key = "participantTotalCount";
            var currentKey = "sushiCurrentCount";

            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userId))
                return false;

            var count = 0;
            if (await _localStorage.ContainKeyAsync(key))
                count = await _localStorage.GetItemAsync<int>(key);

            // Send både count og rating (payload samlet)
            var payload = new Participant
            {
                UserId = userId,
                Count = Math.Max(0, count),
                Rating = rating is null ? null : Math.Clamp(rating.Value, 1, 10)
            };

            var response = await _httpClient.PutAsJsonAsync($"api/Sessions/{sessionId}/participants", payload);
            if (!response.IsSuccessStatusCode) return false;

            // Ryd op i localStorage (mf vigtigt!!!!)
            if (await _localStorage.ContainKeyAsync(key)) await _localStorage.RemoveItemAsync(key);
            if (await _localStorage.ContainKeyAsync(currentKey)) await _localStorage.RemoveItemAsync(currentKey);

            return true;
        }

    }
}