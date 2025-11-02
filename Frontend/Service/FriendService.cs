using System.Net.Http;
using System.Net.Http.Json;
using Core;
using Frontend.Service.IService;

namespace Frontend.Services;

public class FriendService : IFriendService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "api/Friends";

    public FriendService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<User>> GetFriends(string userId)
    {
        var res = await _http.GetFromJsonAsync<List<User>>($"{BaseUrl}/{userId}/friends");
        return res ?? new List<User>();
    }

    public async Task<bool> RemoveFriend(string userId, string friendId)
    {
        var res = await _http.DeleteAsync($"{BaseUrl}/{userId}/{friendId}");
        return res.IsSuccessStatusCode;
    }

    public async Task<List<User>> SearchUsersByName(string term)
    {
        var res = await _http.GetFromJsonAsync<List<User>>($"{BaseUrl}/search?term={Uri.EscapeDataString(term)}");
        return res ?? new List<User>();
    }

    public async Task<bool> SendFriendRequest(string fromUserId, string toUserId)
    {
        var res = await _http.PostAsJsonAsync($"{BaseUrl}/request", new { FromUserId = fromUserId, ToUserId = toUserId });
        return res.IsSuccessStatusCode;
    }

    public async Task<List<FriendRequest>> GetIncomingFriendRequests(string userId)
    {
        var res = await _http.GetFromJsonAsync<List<FriendRequest>>($"{BaseUrl}/{userId}/incoming");
        return res ?? new List<FriendRequest>();
    }

    public async Task<List<FriendRequest>> GetOutgoingFriendRequests(string userId)
    {
        var res = await _http.GetFromJsonAsync<List<FriendRequest>>($"{BaseUrl}/{userId}/outgoing");
        return res ?? new List<FriendRequest>();
    }

    public async Task<bool> RespondToFriendRequest(string requestId, bool accept)
    {
        var res = await _http.PostAsJsonAsync($"{BaseUrl}/respond", new { RequestId = requestId, Accept = accept });
        return res.IsSuccessStatusCode;
    }
}