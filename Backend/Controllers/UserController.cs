using Backend.Repositories.IRepository;
using Core;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")] // -> "api/Users"
public class UsersController : ControllerBase
{
    private readonly IUserRepository _repo;

    public UsersController(IUserRepository repo)
    {
        _repo = repo;
    }

    // GET: api/Users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetAll()
    {
        var list = await _repo.GetAllAsync();
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
        var user = await _repo.FindByEmailAndPasswordHashAsync(email, hashedInputPassword);
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
            var created = await _repo.CreateAsync(newUser);
            // Efter insert har newUser.UserId fået ObjectId som string
            return CreatedAtAction(nameof(GetById), new { userId = created.UserId }, created);
        }
        catch (MongoDB.Driver.MongoWriteException mwx) when (mwx.WriteError?.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
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

        if (!string.IsNullOrWhiteSpace(updated.Password))
        {
            var hashedPassword = PasswordHasherLinearProbing.Hash(updated.Password.Trim());
            updated.Password = hashedPassword;
        }

        var ok = await _repo.UpdateAsync(updated);
        if (!ok) return NotFound();
        return NoContent();
    }

    // GET: api/Users/user/{userId} bruges fra service men fra controllerens opret funk
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<User>> GetById(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");
        var user = await _repo.GetByIdAsync(userId);
        if (user is null) return NotFound();
        return Ok(user);
    }

    // DELETE: api/Users/{userId}
    [HttpDelete("{userId}")]
    public async Task<IActionResult> Delete(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");
        var ok = await _repo.DeleteAsync(userId);
        if (!ok) return NotFound();
        return NoContent();
    }
}
