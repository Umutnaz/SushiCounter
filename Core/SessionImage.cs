using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core;

public class ImageRef
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public string? UploadedBy { get; set; }

    // Marker for thumbnail (first image becomes thumbnail automatically)
    public bool IsThumbnail { get; set; } = false;
}
