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

        // API base til participants ligger under Sessions-controlleren
        private const string BaseSessionsUrl = "api/Sessions";

        // Nøgler i localStorage
        private const string TotalKey = "participantTotalCount";
        private const string CurrentKey = "sushiCurrentCount"; // (ryd op efter commit, hvis brugt andre steder)

        public ParticipantService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
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

        /// <summary>
        /// Lægger <paramref name="delta"/> til den akkumulerede tæller i localStorage.
        /// Bruges fra SushiCounter-siden hver gang man klikker.
        /// Returnerer den nye samlede værdi.
        /// </summary>
        public async Task<int> AddCountToSessionAsync(int delta)
        {
            var total = 0;
            if (await _localStorage.ContainKeyAsync(TotalKey))
                total = await _localStorage.GetItemAsync<int>(TotalKey);

            total = checked(total + delta);
            await _localStorage.SetItemAsync(TotalKey, total);
            return total;
        }

        /// <summary>
        /// Hent aktuel akkumuleret tæller fra localStorage (0 hvis ikke sat).
        /// Bruges til at vise tallet på SushiCounter efter refresh.
        /// </summary>
        public async Task<int> GetCurrentLocalCountAsync()
        {
            if (await _localStorage.ContainKeyAsync(TotalKey))
                return await _localStorage.GetItemAsync<int>(TotalKey);

            return 0;
        }

        public async Task<int> ResetLocalCountAsync()
        {
            if (await _localStorage.ContainKeyAsync(TotalKey))
                await _localStorage.RemoveItemAsync(TotalKey);

            if (await _localStorage.ContainKeyAsync(CurrentKey))
                await _localStorage.RemoveItemAsync(CurrentKey);

            return 0; 
        }
        /// <summary>
        /// Læser den akkumulerede tæller fra localStorage og sender den — samt valgfri rating —
        /// til backend for den valgte session. Nulstiller localStorage ved succes.
        /// </summary>
        public async Task<bool> CommitLocalCountToSessionAsync(string sessionId, string userId, int? rating = null)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userId))
                return false;

            var count = 0;
            if (await _localStorage.ContainKeyAsync(TotalKey))
                count = await _localStorage.GetItemAsync<int>(TotalKey);

            var payload = new Participant
            {
                UserId = userId,
                Count = Math.Max(0, count),
                Rating = rating is null ? null : Math.Clamp(rating.Value, 1, 10)
            };

            var response = await _httpClient.PutAsJsonAsync($"{BaseSessionsUrl}/{sessionId}/participants", payload);
            if (!response.IsSuccessStatusCode) return false;

            // Ryd op efter succes
            if (await _localStorage.ContainKeyAsync(TotalKey)) await _localStorage.RemoveItemAsync(TotalKey);
            if (await _localStorage.ContainKeyAsync(CurrentKey)) await _localStorage.RemoveItemAsync(CurrentKey);

            return true;
        }
    }
}
