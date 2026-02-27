using Backend.Repositories;
using Backend.Repositories.IRepository;
using Core;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")] // => api/Sessions
public class SessionsController : ControllerBase
{
    private readonly ISessionRepository _repo;

    public SessionsController(ISessionRepository repo)
    {
        _repo = repo;
    }

    // GET: api/Sessions/mine/{userId}
    // Henter ALLE sessioner hvor brugeren er creator eller deltager
    [HttpGet("mine/{userId}")]
    public async Task<ActionResult<IEnumerable<Session>>> GetMine(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");
        var list = await _repo.GetMineAsync(userId);
        return Ok(list);
    }

    // GET: api/Sessions/open/{userId}
    // Åbne sessioner for en given bruger (til SushiCounter "vælg session")
    [HttpGet("open/{userId}")]
    public async Task<ActionResult<IEnumerable<Session>>> GetOpenForUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");
        var list = await _repo.GetOpenForUserAsync(userId);
        return Ok(list);
    }

    // GET: api/Sessions/{sessionId}
    [HttpGet("{sessionId}")]
    public async Task<ActionResult<Session>> GetById(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId mangler.");
        var s = await _repo.GetByIdAsync(sessionId);
        if (s is null) return NotFound();

        s.TotalCount = s.Participants?.Sum(p => p.Count) ?? 0;
        var ratings = s.Participants?.Where(p => p.Rating.HasValue).Select(p => p.Rating!.Value).ToList() ?? new();
        s.Rating = ratings.Count == 0 ? null : (int?)Math.Round(ratings.Average());

        return Ok(s);
    }

    // POST: api/Sessions/opret/{creatorUserId}
    [HttpPost("opret/{creatorUserId}")]
    public async Task<ActionResult<Session>> Create(string creatorUserId, [FromBody] Session input)
    {
        if (string.IsNullOrWhiteSpace(creatorUserId)) return BadRequest("creatorUserId mangler.");
        if (input is null) return BadRequest("Body mangler.");
        if (string.IsNullOrWhiteSpace(input.Title)) return BadRequest("Title er påkrævet.");

        var newSession = new Session
        {
            Title = input.Title.Trim(),
            RestaurantName = input.RestaurantName?.Trim(),
            Description = input.Description?.Trim(),
            CreatorId = creatorUserId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Participants = input.Participants ?? new List<Participant>(),
            TotalCount = 0,
            Rating = null
        };

        var created = await _repo.CreateAsync(newSession);
        return CreatedAtAction(nameof(GetById), new { sessionId = created.SessionId }, created);
    }

    // PUT: api/Sessions/update
    // Opdaterer meta + status; ændringer på participants sker i ParticipantsController
    [HttpPut("update")]
    public async Task<IActionResult> Update([FromBody] Session updated)
    {
        if (updated is null || string.IsNullOrWhiteSpace(updated.SessionId))
            return BadRequest("SessionId mangler.");

        var ok = await _repo.UpdateAsync(updated);
        if (!ok) return NotFound();
        return NoContent();
    }

    // DELETE: api/Sessions/{sessionId}
    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> Delete(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId mangler.");
        var ok = await _repo.DeleteSessionAsync(sessionId);
        if (!ok) return NotFound();
        return NoContent();
    }
}
