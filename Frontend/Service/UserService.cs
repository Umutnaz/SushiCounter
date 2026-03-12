﻿using Blazored.LocalStorage;
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
        // ...existing code...
        public async Task<User?> Login(string email, string password)
        {
            // Normaliser email til lowercase for at matche backend (som gemmer email i lowercase)
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var response = await _httpClient.GetAsync($"{BaseURL}/login/{normalizedEmail}/{password}");
            if (!response.IsSuccessStatusCode)
                return null;

            var user = await response.Content.ReadFromJsonAsync<User>();
            if (user != null)
            {
                await _localStorage.SetItemAsync("user", user);
            }
            return user;
        }

        // Gem bruger direkte til localStorage uden API-kald (bruges efter oprettelse)
        public async Task SetUserLoggedIn(User user)
        {
            await _localStorage.SetItemAsync("user", user);
        }

        // ...existing code...


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

             // Gem opdateret bruger til localStorage (ingen ekstra API-kald nødvendigt)
             await _localStorage.SetItemAsync("user", user);
         }
         // henter en specifik bruger baseret på id
         // return: userobjekt hvis fundet, ellers null
         public async Task<User?> GetUserByUserId(string userId)
         {
             return await _httpClient.GetFromJsonAsync<User>($"{BaseURL}/user/{userId}");
         }
        
        // sletter en bruger fra databasen
        // param: userId - id på brugeren der skal slettes
        public async Task DeleteUserAsync(string userId)
        {
            await _localStorage.RemoveItemAsync("user");
            var response = await _httpClient.DeleteAsync($"{BaseURL}/{userId}");
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException("Kunne ikke slette bruger.");
            }
        }

    }
}
