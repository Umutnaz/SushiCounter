using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core
{
    public enum FriendRequestStatus
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2
    }

    public class FriendRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? RequestId { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string FromUserId { get; set; } = default!;

        [BsonRepresentation(BsonType.ObjectId)]
        public string ToUserId { get; set; } = default!;

        public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RespondedAt { get; set; }

        // Valgfrit: Denormaliseret visning i frontend (lagres ikke i Mongo)
        [BsonIgnore] public User? FromUser { get; set; }
        [BsonIgnore] public User? ToUser { get; set; }
    }
}