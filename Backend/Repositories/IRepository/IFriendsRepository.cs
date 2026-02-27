using Core;

namespace Backend.Repositories.IRepository;

public interface IFriendsRepository
{
    Task<List<User>> SearchAsync(string? term);
    Task<List<User>> GetFriendsAsync(string userId);
    Task<bool> RemoveFriendAsync(string userId, string friendId);
    Task<FriendRequest?> SendRequestAsync(string fromUserId, string toUserId);
    Task<bool> RespondAsync(string requestId, bool accept);
    Task<List<FriendRequest>> GetIncomingAsync(string userId);
    Task<List<FriendRequest>> GetOutgoingAsync(string userId);
}
