using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core;

public class Session
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? SessionId { get; set; }
    [Required(ErrorMessage = "Du skal tilføje en titel til sessionen.")]
    public string Title { get; set; } = string.Empty; //Titlen på sessionen
    public string? RestaurantName { get; set; } //Navnet på restauranten
    public string?  Description { get; set; } //Beskrivelse af sessionen
    [BsonRepresentation(BsonType.ObjectId)]
    public string? CreatorId { get; set; }

    public int TotalCount { get; set; } = 0; //Total sushi count for sessionen fra alle deltagere
    public int? Rating { get; set; } //Gennemsnitlig rating for sessionen fra alle deltagere
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; //Hvornår sessionen blev oprettet
    public List<Participant> Participants { get; set; }  = new(); //Liste over sushi count for hver bruger i sessionen
    
    public bool IsActive { get; set; } = true; //Om sessionen er aktiv eller afsluttet

    // Liste af billeder (referencer til GridFS filer)
    public List<ImageRef> Images { get; set; } = new();
}

//Her er det overordnet for en session, info for hvert enkelte user er under AttentednCounts
//Kun id behøver at være unik
//titel skal udfyldes ved oprettelse
//resturantname, description og rating er valgfrie felter
//EatedWith er en liste over brugere der har deltaget i sessionen
//Man kan ved oprettelse, eller ved redigering af sessionen, vælge at tilføje venner til EatedWith listen
//CreatedAt sættes automatisk ved oprettelse af sessionen
//SessionCreater er den bruger der har oprettet sessionen