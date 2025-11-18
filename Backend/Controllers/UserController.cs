using System.Text.RegularExpressions;
using Core;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Backend; //hashed fil

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")] // -> "api/Users"
public class UsersController : ControllerBase
{
    private readonly IMongoCollection<User> _users;

    // Skift evt. disse tre værdier til appsettings.json + DI, når du vil rydde op
    private const string UsersCollection = "Users";

    public UsersController()
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
                               ?? throw new InvalidOperationException("MONGO_CONNECTION_STRING is not set.");
        var databaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME")
                           ?? throw new InvalidOperationException("MONGO_DATABASE_NAME is not set.");

        var client = new MongoClient(connectionString);
        var db = client.GetDatabase(databaseName);
        _users = db.GetCollection<User>(UsersCollection);

        // Sørg for unikke indeks på Email og Name
        var indexKeys = Builders<User>.IndexKeys;
        var emailIndex = new CreateIndexModel<User>(indexKeys.Ascending(u => u.Email),
            new CreateIndexOptions { Unique = true, Name = "ux_users_email" });
        var nameIndex = new CreateIndexModel<User>(indexKeys.Ascending(u => u.Name),
            new CreateIndexOptions { Unique = true, Name = "ux_users_name" });
        _users.Indexes.CreateMany(new[] { emailIndex, nameIndex });
    }

    // GET: api/Users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetAll()
    {
        var list = await _users.Find(_ => true).ToListAsync();
        return Ok(list);
    }
// GET: api/Users/login/{email}/{password}
//email er case-insensitive
// password er case-sensitive
// brugernavn er ikke med i login, derfor ligemeget.. skal man ændre det gør man det bare i profilen (mypage)
    [HttpGet("login/{email}/{password}")]
    public async Task<ActionResult<User>> Login(string email, string password)
    {
        // 1) Vi laver den samme hash på det password, som brugeren skriver ved login,
        //    som vi laver ved oprettelse. Så sammenligner vi "hashet mod hash".
        //    → Vi skal ALDRIG forsøge at "un-hashe".
        var hashedInputPassword = PasswordHasherLinearProbing.Hash(password);

        // 2) Nu sammenligner vi den hash, vi lige har regnet, med hash-værdien i databasen.
        var filter = Builders<User>.Filter.And(
            // Password gemmes i DB som hash (kun tal i en streng),
            // så vi søger på den hash-værdi:
            Builders<User>.Filter.Eq(u => u.Password, hashedInputPassword),

            // Email er case-insensitive altså at alt bliver småt
            Builders<User>.Filter.Regex(
                u => u.Email,
                new MongoDB.Bson.BsonRegularExpression($"^{Regex.Escape(email)}$", "i")
            )
        );

        var user = await _users.Find(filter).FirstOrDefaultAsync();
        if (user is null) return NotFound();

        return Ok(user);
    }

// POST: api/Users/opret
    [HttpPost("opret")]
    public async Task<ActionResult<User>> Opret([FromBody] User input)
    {
        if (input is null) return BadRequest("Body mangler.");
        if (string.IsNullOrWhiteSpace(input.Name) ||
            string.IsNullOrWhiteSpace(input.Email) ||
            string.IsNullOrWhiteSpace(input.Password))
        {
            return BadRequest("Name, Email og Password er påkrævet.");
        }

        // Her laver vi hash'en, INDEN vi gemmer brugeren:
        // 1) Vi trimmer password → fjerner utilsigtede mellemrum før/efter.
        // 2) Vi laver hash → output er en streng bestående kun af tal,
        //    to cifre pr. tegn i det oprindelige password.
        var hashedPassword = PasswordHasherLinearProbing.Hash(input.Password.Trim());

        var now = DateTime.UtcNow;

        var newUser = new User
        {
            // UserId må være null/empty – Mongo sætter ObjectId
            Name = input.Name.Trim(),
            Email = input.Email.Trim().ToLowerInvariant(), // email gemmes i lowercase
            Password = hashedPassword,                     // GEM ALTID HASH, ALDRIG klartekst
            CreatedAt = now,
            UpdatedAt = now,
            Sessions = new List<Session>()
        };

        try
        {
            await _users.InsertOneAsync(newUser);
            // Efter insert har newUser.UserId fået ObjectId som string
            return CreatedAtAction(nameof(GetById), new { userId = newUser.UserId }, newUser);
        }
        catch (MongoWriteException mwx) when (mwx.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Unik constraint rammer – find hvilken
            if (mwx.Message.Contains("ux_users_email", StringComparison.OrdinalIgnoreCase))
                return Conflict("Email findes allerede.");
            if (mwx.Message.Contains("ux_users_name", StringComparison.OrdinalIgnoreCase))
                return Conflict("Brugernavn findes allerede.");
            return Conflict("Bruger findes allerede.");
        }
    }


    // PUT: api/Users/update
    // (simpel profil-opdatering – password håndteres ikke her)
    [HttpPut("update")]
    public async Task<IActionResult> Update([FromBody] User updated)
    {
        if (updated is null || string.IsNullOrWhiteSpace(updated.UserId))
            return BadRequest("UserId mangler.");

        var filter = Builders<User>.Filter.Eq(u => u.UserId, updated.UserId);
        var update = Builders<User>.Update
            .Set(u => u.Name, updated.Name)
            .Set(u => u.Email, updated.Email.ToLowerInvariant())
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        try
        {
            var result = await _users.UpdateOneAsync(filter, update);
            if (result.MatchedCount == 0) return NotFound();
            return NoContent();
        }
        catch (MongoWriteException mwx) when (mwx.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            if (mwx.Message.Contains("ux_users_email", StringComparison.OrdinalIgnoreCase))
                return Conflict("Email findes allerede.");
            if (mwx.Message.Contains("ux_users_name", StringComparison.OrdinalIgnoreCase))
                return Conflict("Brugernavn findes allerede.");
            return Conflict("Unikhedsfejl.");
        }
    }

    // GET: api/Users/maxid
    // (Din service forventer en int – vi returnerer antal brugere)
    [HttpGet("maxid")]
    public async Task<ActionResult<int>> GetMaxId()
    {
        var count = (int)await _users.CountDocumentsAsync(_ => true);
        return Ok(count);
    }

    // GET: api/Users/user/{userId}
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<User>> GetById(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");
        var user = await _users.Find(u => u.UserId == userId).FirstOrDefaultAsync();
        if (user is null) return NotFound();
        return Ok(user);
    }

    // DELETE: api/Users/{userId}
    [HttpDelete("{userId}")]
    public async Task<IActionResult> Delete(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");
        var result = await _users.DeleteOneAsync(u => u.UserId == userId);
        if (result.DeletedCount == 0) return NotFound();
        return NoContent();
    }
}
