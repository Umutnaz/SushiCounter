using Backend.Repositories;
using Backend.Repositories.IRepository;
using Core;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Backend.Controllers;

[ApiController]
[Route("api/Sessions/{sessionId}/images")]
public class SessionImagesController : ControllerBase
{
    private readonly ISessionRepository _repo;

    public SessionImagesController(ISessionRepository repo)
    {
        _repo = repo;
    }

    // POST api/Sessions/{sessionId}/images
    [HttpPost]
    public async Task<IActionResult> Upload(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId mangler.");
        var session = await _repo.GetByIdAsync(sessionId);
        if (session is null) return NotFound("Session ikke fundet.");

        // Optional owner check: if X-User-Id header is provided, require that it's the creator
        var requester = Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(requester) && requester != session.CreatorId)
            return Forbid();

        if (!Request.HasFormContentType || !Request.Form.Files.Any())
            return BadRequest("Ingen fil uploadet. Benyt multipart/form-data med en 'file' felt.");

        var file = Request.Form.Files[0];
        if (file.Length == 0) return BadRequest("Tom fil.");

        // Basic validation - kun billeder
        var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowed.Contains(file.ContentType)) return BadRequest("Kun billedfiler (.jpg, .png, .webp) er tilladt.");

        using var stream = file.OpenReadStream();
        var image = await _repo.UploadImageAsync(sessionId, file.FileName, stream, file.ContentType, requester);
        if (image is null) return StatusCode(500, "Kunne ikke gemme billedet.");

        return CreatedAtAction(nameof(Download), new { sessionId = sessionId, imageId = image.Id }, image);
    }

    // GET api/Sessions/{sessionId}/images/{imageId}
    [HttpGet("{imageId}")]
    public async Task<IActionResult> Download(string sessionId, string imageId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(imageId)) return BadRequest("Manglende id.");
        var session = await _repo.GetByIdAsync(sessionId);
        if (session is null) return NotFound("Session ikke fundet.");

        var found = session.Images?.FirstOrDefault(i => i.Id == imageId);
        if (found is null) return NotFound("Billedet er ikke fundet for denne session.");

        try
        {
            var (stream, contentType, fileName) = await _repo.DownloadImageAsync(imageId);
            return File(stream, contentType, fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound("Filen ikke fundet i GridFS.");
        }
    }

    // DELETE api/Sessions/{sessionId}/images/{imageId}
    [HttpDelete("{imageId}")]
    public async Task<IActionResult> Delete(string sessionId, string imageId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(imageId)) return BadRequest("Manglende id.");
        var session = await _repo.GetByIdAsync(sessionId);
        if (session is null) return NotFound("Session ikke fundet.");

        // Optional owner check: if X-User-Id header is provided, require that it's the creator
        var requester = Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(requester) && requester != session.CreatorId)
            return Forbid();

        var ok = await _repo.DeleteImageAsync(sessionId, imageId);
        if (!ok) return NotFound("Billedet findes ikke eller kunne ikke slettes.");
        return NoContent();
    }

    // PUT api/Sessions/{sessionId}/images/{imageId}/thumbnail
    [HttpPut("{imageId}/thumbnail")]
    public async Task<IActionResult> SetThumbnail(string sessionId, string imageId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(imageId)) return BadRequest("Manglende id.");
        var session = await _repo.GetByIdAsync(sessionId);
        if (session is null) return NotFound("Session ikke fundet.");

        // Optional owner check: if X-User-Id header is provided, require that it's the creator
        var requester = Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(requester) && requester != session.CreatorId)
            return Forbid();

        var found = session.Images?.FirstOrDefault(i => i.Id == imageId);
        if (found is null) return NotFound("Billedet findes ikke for denne session.");

        var ok = await _repo.SetImageThumbnailAsync(sessionId, imageId);
        if (!ok) return StatusCode(500, "Kunne ikke sætte thumbnail.");
        return NoContent();
    }
}
