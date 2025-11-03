using Core;
namespace Frontend.Service.IService;
public interface IFriendService
{
    Task<List<User>> GetFriends(string userId);
    Task<bool> RemoveFriend(string userId, string friendId);

    Task<List<User>> SearchUsersByName(string term);

    Task<bool> SendFriendRequest(string fromUserId, string toUserId);

    Task<List<FriendRequest>> GetIncomingFriendRequests(string userId);
    Task<List<FriendRequest>> GetOutgoingFriendRequests(string userId);

    Task<bool> RespondToFriendRequest(string requestId, bool accept);
}