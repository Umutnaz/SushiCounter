using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core;

public class Participant
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string SessionId { get; set; } = null!;
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;
    public int? Count { get; set; }
    public int? Rating { get; set; } //Rating fra 1-10 (stjerner i frontend)
}

// Her er hvad hver inviduel bringer til en session
// Her er ikke id da man kan bruge UserId og SessionId som en sammensat nøgle
// Count er antal sushi stykker brugeren har spist i sessionen
// Rating er brugerens vurdering af sessionen fra 1-10 (stjerner i frontend)
// User er den specifikke bruger der har deltaget i sessionen
// Session er den specifikke session brugeren har deltaget i
