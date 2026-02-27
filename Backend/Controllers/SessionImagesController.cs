using Backend.Repositories;
using Backend.Repositories.IRepository;
using Core;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Microsoft.Extensions.Logging;

namespace Backend.Controllers;

[ApiController]
[Route("api/Sessions/{sessionId}/images")]
public class SessionImagesController : ControllerBase
{
    private readonly ISessionRepository _repo;
    private readonly ILogger<SessionImagesController> _logger;

    public SessionImagesController(ISessionRepository repo, ILogger<SessionImagesController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // POST api/Sessions/{sessionId}/images
    [HttpPost]
    public async Task<IActionResult> Upload(string sessionId)
    {
        _logger.LogInformation("Upload called for sessionId={sessionId}", sessionId);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            _logger.LogWarning("Upload failed: missing sessionId");
            return BadRequest("sessionId mangler.");
        }
        var session = await _repo.GetByIdAsync(sessionId);
        if (session is null)
        {
            _logger.LogWarning("Upload failed: session not found {sessionId}", sessionId);
            return NotFound("Session ikke fundet.");
        }

        // Optional owner check: if X-User-Id header is provided, require that it's the creator
        var requester = Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(requester) && requester != session.CreatorId)
        {
            _logger.LogWarning("Upload forbidden: requester {requester} is not creator {creator}", requester, session.CreatorId);
            return Forbid();
        }

        if (!Request.HasFormContentType || !Request.Form.Files.Any())
        {
            _logger.LogWarning("Upload failed: no file in request for session {sessionId}", sessionId);
            return BadRequest("Ingen fil uploadet. Benyt multipart/form-data med en 'file' felt.");
        }

        var file = Request.Form.Files[0];
        if (file.Length == 0)
        {
            _logger.LogWarning("Upload failed: empty file for session {sessionId}", sessionId);
            return BadRequest("Tom fil.");
        }

        // Basic validation - kun billeder
        var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowed.Contains(file.ContentType))
        {
            _logger.LogWarning("Upload failed: invalid content-type {ct} for session {sessionId}", file.ContentType, sessionId);
            return BadRequest("Kun billedfiler (.jpg, .png, .webp) er tilladt.");
        }

        using var stream = file.OpenReadStream();
        var image = await _repo.UploadImageAsync(sessionId, file.FileName, stream, file.ContentType, requester);
        if (image is null)
        {
            _logger.LogError("Upload failed: repository could not save image for session {sessionId}", sessionId);
            return StatusCode(500, "Kunne ikke gemme billedet.");
        }

        _logger.LogInformation("Upload succeeded: imageId={imageId} for session {sessionId}", image.Id, sessionId);
        return CreatedAtAction(nameof(Download), new { sessionId = sessionId, imageId = image.Id }, image);
    }

    // GET api/Sessions/{sessionId}/images/{imageId}
    [HttpGet("{imageId}")]
    public async Task<IActionResult> Download(string sessionId, string imageId)
    {
        _logger.LogInformation("Download called for sessionId={sessionId} imageId={imageId}", sessionId, imageId);
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(imageId))
        {
            _logger.LogWarning("Download failed: missing ids");
            return BadRequest("Manglende id.");
        }
        var session = await _repo.GetByIdAsync(sessionId);
        if (session is null)
        {
            _logger.LogWarning("Download failed: session not found {sessionId}", sessionId);
            return NotFound("Session ikke fundet.");
        }

        var found = session.Images?.FirstOrDefault(i => i.Id == imageId);
        if (found is null)
        {
            _logger.LogWarning("Download failed: image {imageId} not found in session {sessionId}", imageId, sessionId);
            return NotFound("Billedet er ikke fundet for denne session.");
        }

        try
        {
            var (stream, contentType, fileName) = await _repo.DownloadImageAsync(imageId);
            _logger.LogInformation("Download succeeded: image {imageId} contentType={ct}", imageId, contentType);
            return File(stream, contentType, fileName);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Download failed: GridFS file not found for image {imageId}", imageId);
            return NotFound("Filen ikke fundet i GridFS.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed: unexpected error for image {imageId}", imageId);
            return StatusCode(500, "Fejl ved hentning af billedet.");
        }
    }

    // DELETE api/Sessions/{sessionId}/images/{imageId}
    [HttpDelete("{imageId}")]
    public async Task<IActionResult> Delete(string sessionId, string imageId)
    {
        _logger.LogInformation("Delete called for sessionId={sessionId} imageId={imageId}", sessionId, imageId);
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(imageId))
        {
            _logger.LogWarning("Delete failed: missing ids");
            return BadRequest("Manglende id.");
        }
        var session = await _repo.GetByIdAsync(sessionId);
        if (session is null)
        {
            _logger.LogWarning("Delete failed: session not found {sessionId}", sessionId);
            return NotFound("Session ikke fundet.");
        }

        // Optional owner check: if X-User-Id header is provided, require that it's the creator
        var requester = Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(requester) && requester != session.CreatorId)
        {
            _logger.LogWarning("Delete forbidden: requester {requester} is not creator {creator}", requester, session.CreatorId);
            return Forbid();
        }

        var ok = await _repo.DeleteImageAsync(sessionId, imageId);
        if (!ok)
        {
            _logger.LogWarning("Delete failed: repository could not delete image {imageId} for session {sessionId}", imageId, sessionId);
            return NotFound("Billedet findes ikke eller kunne ikke slettes.");
        }

        _logger.LogInformation("Delete succeeded: image {imageId} removed from session {sessionId}", imageId, sessionId);
        return NoContent();
    }

    // PUT api/Sessions/{sessionId}/images/{imageId}/thumbnail
    [HttpPut("{imageId}/thumbnail")]
    public async Task<IActionResult> SetThumbnail(string sessionId, string imageId)
    {
        _logger.LogInformation("SetThumbnail called for sessionId={sessionId} imageId={imageId}", sessionId, imageId);
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(imageId))
        {
            _logger.LogWarning("SetThumbnail failed: missing ids");
            return BadRequest("Manglende id.");
        }
        var session = await _repo.GetByIdAsync(sessionId);
        if (session is null)
        {
            _logger.LogWarning("SetThumbnail failed: session not found {sessionId}", sessionId);
            return NotFound("Session ikke fundet.");
        }

        // Optional owner check: if X-User-Id header is provided, require that it's the creator
        var requester = Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(requester) && requester != session.CreatorId)
        {
            _logger.LogWarning("SetThumbnail forbidden: requester {requester} is not creator {creator}", requester, session.CreatorId);
            return Forbid();
        }

        var found = session.Images?.FirstOrDefault(i => i.Id == imageId);
        if (found is null)
        {
            _logger.LogWarning("SetThumbnail failed: image {imageId} not found in session {sessionId}", imageId, sessionId);
            return NotFound("Billedet findes ikke for denne session.");
        }

        var ok = await _repo.SetImageThumbnailAsync(sessionId, imageId);
        if (!ok)
        {
            _logger.LogError("SetThumbnail failed: repository could not set thumbnail for image {imageId} in session {sessionId}", imageId, sessionId);
            return StatusCode(500, "Kunne ikke sætte thumbnail.");
        }

        _logger.LogInformation("SetThumbnail succeeded: image {imageId} set as thumbnail for session {sessionId}", imageId, sessionId);
        return NoContent();
    }
}
