using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core;

public class Session
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string? SessionId { get; set; }
    public string Title { get; set; } //Titlen på sessionen
    public string? RestaurantName { get; set; } //Navnet på restauranten
    public string?  Description { get; set; } //Beskrivelse af sessionen
    public DateTime CreatedAt { get; set; } //Hvornår sessionen blev oprettet
    public List<Participant> Participants { get; set; }  = new();//Liste over sushi count for hver bruger i sessionen
}

//Her er det overordnet for en session, info for hvert enkelte user er under AttentednCounts
//Kun id behøver at være unik
//titel skal udfyldes ved oprettelse
//resturantname, description og rating er valgfrie felter
//EatedWith er en liste over brugere der har deltaget i sessionen
//Man kan ved oprettelse, eller ved redigering af sessionen, vælge at tilføje venner til EatedWith listen
//CreatedAt sættes automatisk ved oprettelse af sessionen
//SessionCreater er den bruger der har oprettet sessionen