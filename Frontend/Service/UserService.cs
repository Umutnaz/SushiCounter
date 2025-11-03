using Blazored.LocalStorage;
using Core;
using System.Net.Http;
using System.Net.Http.Json;
using Frontend.Service.IService;

namespace Frontend.Services
{
    public class UserService : IUserService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private string BaseURL = "api/Users";

        public UserService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }
        
        // henter alle brugere fra databasen
        // return: liste med alle brugere
        public async Task<List<User>> GetAllUsersAsync()
        {
            var users = await _httpClient.GetFromJsonAsync<List<User>>(BaseURL);
            return users ?? new List<User>();
        }
        
        // forsøger at logge en bruger ind med e-mail og password
        // return: userobjekt hvis login er succesfuldt, ellers null
        public async Task<User?> Login(string email, string password)
        {
            var response = await _httpClient.GetAsync($"{BaseURL}/login/{email}/{password}");
            if (!response.IsSuccessStatusCode)
                return null;

            var user = await response.Content.ReadFromJsonAsync<User>();
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                await _localStorage.SetItemAsync("user", user);
                return user;
            }

            return null;
        }

        public async Task Logout()
        {
            await _localStorage.RemoveItemAsync("user");
        }
        // henter den aktuelt loggede bruger fra local storage
        // return: userobjekt hvis en bruger er logget ind, ellers null
        public async Task<User?> GetUserLoggedInAsync()
        {
            return await _localStorage.GetItemAsync<User>("user");
        }

        // opretter en ny bruger i databasen
        // return: det oprettede userobjekt eller null hvis oprettelsen fejler
        public async Task<User?> AddUserAsync(User user)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BaseURL}/opret", user);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(error);
            }


            var createdUser = await response.Content.ReadFromJsonAsync<User>();
            return createdUser;
        }

        // opdaterer en eksisterende bruger i databasen
        // param: user, brugerens data som opdateres
        public async Task UpdateUser(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var response = await _httpClient.PutAsJsonAsync($"{BaseURL}/update", user);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();

                // Smid en mere præcis fejl som kan fanges i MyPage.razor
                if (error.Contains("Brugernavn findes allerede", StringComparison.OrdinalIgnoreCase))
                    throw new HttpRequestException("Brugernavnet er allerede valgt.");
                else
                    throw new HttpRequestException("Kunne ikke gemme ændringer. Prøv igen.");
            }

            // Hent frisk kopi fra backend (så vi får evt. normaliseret email/UpdatedAt m.m.)
            var fresh = await GetUserByUserId(user.UserId);
            await _localStorage.SetItemAsync("user", fresh ?? user);
        }


        // logger brugeren ud ved at fjerne brugerdata fra local storage
        
        // henter det højeste bruger-id fra databasen
        // return: største id som int
        public async Task<int> GetMaxUserId()
        {
            return await _httpClient.GetFromJsonAsync<int>($"{BaseURL}/maxid");
        }

        // henter en specifik bruger baseret på id
        // return: userobjekt hvis fundet, ellers null
        public async Task<User?> GetUserByUserId(string userId)
        {
            Console.WriteLine($"Returning user: {userId}: service");
            return await _httpClient.GetFromJsonAsync<User>($"{BaseURL}/user/{userId}");
        }
        
        // sletter en bruger fra databasen
        // param: userId - id på brugeren der skal slettes
        public async Task DeleteUserAsync(string userId)
        {
            await _httpClient.DeleteAsync($"{BaseURL}/{userId}");
        }

    }
}
