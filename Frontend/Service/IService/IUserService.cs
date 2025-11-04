using Core;
namespace Frontend.Service.IService;
public interface IUserService
{
    Task<User?> Login(string email, string password);  // Log ind med email og password
    Task<User?> AddUserAsync(User user);  // Opret en ny bruger, hvis den ikke findes
    Task Logout();  // Log ud
    Task<User?> GetUserLoggedInAsync();  // Hent den bruger, der er logget ind lige nu
    Task UpdateUser(User user);
    Task DeleteUserAsync(string UserId);

}