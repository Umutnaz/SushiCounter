using Backend.Repositories.IRepository;
using Core;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FriendsController : ControllerBase
{
    private readonly IFriendsRepository _repo;

    public FriendsController(IFriendsRepository repo)
    {
        _repo = repo;
    }

    // GET: api/Friends/search?term=marie1
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<User>>> Search([FromQuery] string term)
    {
        var list = await _repo.SearchAsync(term);
        return Ok(list);
    }

    // GET: api/Friends/{userId}/friends
    [HttpGet("{userId}/friends")]
    public async Task<ActionResult<IEnumerable<User>>> GetFriends(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");
        var friends = await _repo.GetFriendsAsync(userId);
        return Ok(friends);
    }

    // DELETE: api/Friends/{userId}/{friendId}
    [HttpDelete("{userId}/{friendId}")]
    public async Task<IActionResult> RemoveFriend(string userId, string friendId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(friendId))
            return BadRequest("userId/friendId mangler.");

        var ok = await _repo.RemoveFriendAsync(userId, friendId);
        if (!ok) return NotFound();
        return NoContent();
    }

    public record SendRequestDto(string FromUserId, string ToUserId);

    // POST: api/Friends/request
    [HttpPost("request")]
    public async Task<ActionResult<FriendRequest>> SendRequest([FromBody] SendRequestDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.FromUserId) || string.IsNullOrWhiteSpace(dto.ToUserId))
            return BadRequest("FromUserId/ToUserId mangler.");
        if (dto.FromUserId == dto.ToUserId) return BadRequest("Kan ikke ansøge dig selv.");

        var req = await _repo.SendRequestAsync(dto.FromUserId, dto.ToUserId);
        if (req is null) return Conflict("Kunne ikke oprette anmodning eller allerede venner/afventer.");
        return Ok(req);
    }

    public record RespondDto(string RequestId, bool Accept);

    // POST: api/Friends/respond
    [HttpPost("respond")]
    public async Task<IActionResult> Respond([FromBody] RespondDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.RequestId)) return BadRequest("RequestId mangler.");

        var ok = await _repo.RespondAsync(dto.RequestId, dto.Accept);
        if (!ok) return NotFound("Anmodningen ikke fundet eller allerede håndteret.");
        return NoContent();
    }

    // GET: api/Friends/{userId}/incoming
    [HttpGet("{userId}/incoming")]
    public async Task<ActionResult<IEnumerable<FriendRequest>>> Incoming(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");
        var reqs = await _repo.GetIncomingAsync(userId);
        return Ok(reqs);
    }

    // GET: api/Friends/{userId}/outgoing
    [HttpGet("{userId}/outgoing")]
    public async Task<ActionResult<IEnumerable<FriendRequest>>> Outgoing(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId mangler.");
        var reqs = await _repo.GetOutgoingAsync(userId);
        return Ok(reqs);
    }
}
