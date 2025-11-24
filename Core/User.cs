using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? UserId { get; set; } // MongoDB ObjectId som string
    public string Name { get; set; } //Brugernavn synligt for alle
    public string Email { get; set; } //Brugerens email, skal være unik
    [StringLength(100, ErrorMessage = "Password må maks være 100 tegn langt.")]
    public string Password { get; set; } //Brugerens password, hashes i controller
    public DateTime CreatedAt { get; set; } //Hvornår brugeren blev oprettet
    public DateTime UpdatedAt { get; set; } //Hvornår brugeren sidst blev opdateret
    public List<User> Friends { get; set; } = new(); //Liste over brugerens venner
    public List<Session> Sessions { get; set; } = new(); //Liste over sessioner brugeren har oprettet
    public List<FriendRequest> IncomingRequests { get; set; } = new(); //kræver at det er her for at det kan embeddes i mongodb
    public List<FriendRequest> OutgoingRequests { get; set; } = new();
    
}

//Navn, email og password skal udfyldes ved oprettelse
//Email, Password og Name skal være unik for hver bruger
//CreatedAt sættes automatisk ved oprettelse af brugeren
//UpdatedAt opdateres automatisk ved ændring af brugerens oplysninger
//Sessions er en liste over sessioner brugeren har oprettet
