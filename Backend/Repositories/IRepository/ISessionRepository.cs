using Core;
using System.IO;

namespace Backend.Repositories.IRepository;

public interface ISessionRepository
{
    Task<List<Session>> GetMineAsync(string userId);
    Task<List<Session>> GetOpenForUserAsync(string userId);
    Task<Session?> GetByIdAsync(string sessionId);
    Task<Session> CreateAsync(Session s);
    Task<bool> UpdateAsync(Session s);
    Task<bool> DeleteSessionAsync(string sessionId);

    // Image operations
    Task<ImageRef?> UploadImageAsync(string sessionId, string fileName, Stream stream, string contentType, string? uploadedBy);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadImageAsync(string fileId);
    Task<bool> DeleteImageAsync(string sessionId, string imageId);
    Task<bool> SetImageThumbnailAsync(string sessionId, string imageId);
}
