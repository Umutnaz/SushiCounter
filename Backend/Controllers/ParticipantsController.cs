using Backend.Repositories.IRepository;
using Core;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/Sessions/{sessionId}/participants")]
public class ParticipantsController : ControllerBase
{
    private readonly IParticipantsRepository _repo;

    public ParticipantsController(IParticipantsRepository repo)
    {
        _repo = repo;
    }

    // DELETE: api/Sessions/{sessionId}/participants/{userId}
    // Fjerner en deltager og opdaterer total count og rating
    [HttpDelete("{userId}")]
    public async Task<IActionResult> Remove(string sessionId, string userId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userId))
            return BadRequest("sessionId/userId mangler.");

        var ok = await _repo.RemoveParticipantAsync(sessionId, userId);
        if (!ok) return NotFound("Session eller participant ikke fundet.");
        return NoContent();
    }

    // PUT: api/Sessions/{sessionId}/participants
    // Tilføjer eller opdaterer en deltager, plusser counts og opdaterer rating og total
    [HttpPut]
    public async Task<IActionResult> AddOrUpdate(string sessionId, [FromBody] Participant p)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId mangler.");
        if (p is null || string.IsNullOrWhiteSpace(p.UserId)) return BadRequest("Participant mangler.");

        var ok = await _repo.AddOrUpdateParticipantAsync(sessionId, p);
        if (!ok) return NotFound("Session ikke fundet eller handler ikke pga. lukket session.");
        return NoContent();
    }
}
