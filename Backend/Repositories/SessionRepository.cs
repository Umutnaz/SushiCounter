﻿// SessionRepository: Responsible for all database operations related to Session documents
// - Encapsulates MongoDB queries/updates against the Sessions collection
// - Manages GridFS uploads/downloads/deletes for session images
// - Implements simple thumbnail promotion logic when images are removed
// Keep this class small and focused on data access; controllers should only call these methods.

using Backend.Repositories.IRepository;
using Core;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Backend.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly IMongoCollection<Session> _sessions;
    private readonly GridFSBucket _bucket;
    private readonly ILogger<SessionRepository>? _logger;

    public SessionRepository(IMongoClient client, ILogger<SessionRepository>? logger = null)
    {
        var databaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME")
                           ?? throw new InvalidOperationException("MONGO_DATABASE_NAME is not set.");
        var db = client.GetDatabase(databaseName);
        _sessions = db.GetCollection<Session>("Sessions");
        _bucket = new GridFSBucket(db);
        _logger = logger;
    }

    public async Task<List<Session>> GetMineAsync(string userId)
    {
        var filter = Builders<Session>.Filter.Or(
            Builders<Session>.Filter.Eq(s => s.CreatorId, userId),
            Builders<Session>.Filter.ElemMatch(s => s.Participants, p => p.UserId == userId)
        );
        var list = await _sessions.Find(filter).SortByDescending(s => s.CreatedAt).ToListAsync();
        return list;
    }

    public async Task<List<Session>> GetOpenForUserAsync(string userId)
    {
        var filter = Builders<Session>.Filter.And(
            Builders<Session>.Filter.Eq(s => s.IsActive, true),
            Builders<Session>.Filter.Or(
                Builders<Session>.Filter.Eq(s => s.CreatorId, userId),
                Builders<Session>.Filter.ElemMatch(s => s.Participants, p => p.UserId == userId)
            )
        );

        var list = await _sessions.Find(filter).SortByDescending(s => s.CreatedAt).ToListAsync();
        return list;
    }

    public async Task<Session?> GetByIdAsync(string sessionId)
    {
        return await _sessions.Find(s => s.SessionId == sessionId).FirstOrDefaultAsync();
    }

    public async Task<Session> CreateAsync(Session s)
    {
        await _sessions.InsertOneAsync(s);
        return s;
    }

    public async Task<bool> UpdateAsync(Session s)
    {
        var update = Builders<Session>.Update
            .Set(x => x.Title, s.Title)
            .Set(x => x.RestaurantName, s.RestaurantName)
            .Set(x => x.Description, s.Description)
            .Set(x => x.IsActive, s.IsActive);

        var res = await _sessions.UpdateOneAsync(x => x.SessionId == s.SessionId, update);
        return res.MatchedCount > 0;
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        var session = await GetByIdAsync(sessionId);
        if (session is null) return false;

        // delete images from GridFS
        if (session.Images is not null)
        {
            foreach (var img in session.Images)
            {
                if (string.IsNullOrWhiteSpace(img.Id)) continue;
                try
                {
                    await _bucket.DeleteAsync(ObjectId.Parse(img.Id));
                }
                catch { /* ignore */ }
            }
        }

        var res = await _sessions.DeleteOneAsync(x => x.SessionId == sessionId);
        return res.DeletedCount > 0;
    }

    public async Task<ImageRef?> UploadImageAsync(string sessionId, string fileName, Stream stream, string contentType, string? uploadedBy)
    {
        _logger?.LogInformation("Repository UploadImageAsync called for session {sessionId} filename={fileName} contentType={contentType}", sessionId, fileName, contentType);
        var session = await GetByIdAsync(sessionId);
        if (session is null) return null;

        var options = new GridFSUploadOptions
        {
            Metadata = new BsonDocument { { "contentType", contentType ?? "application/octet-stream" }, { "uploadedAt", DateTime.UtcNow } }
        };

        var id = await _bucket.UploadFromStreamAsync(fileName, stream, options);
        var fileId = id.ToString();

        var isThumb = session.Images is null || session.Images.Count == 0;

        var imageRef = new ImageRef
        {
            Id = fileId,
            FileName = fileName,
            ContentType = contentType,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = uploadedBy,
            IsThumbnail = isThumb
        };

        await _sessions.UpdateOneAsync(s => s.SessionId == sessionId, Builders<Session>.Update.Push(s => s.Images, imageRef));
        _logger?.LogInformation("Repository UploadImageAsync stored file {fileId} for session {sessionId}", fileId, sessionId);
        return imageRef;
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadImageAsync(string fileId)
    {
        _logger?.LogInformation("Repository DownloadImageAsync called for fileId={fileId}", fileId);
        var oid = ObjectId.Parse(fileId);
        var info = await _bucket.Find(Builders<GridFSFileInfo>.Filter.Eq(f => f.Id, oid)).FirstOrDefaultAsync();
        if (info is null) {
            _logger?.LogWarning("Repository DownloadImageAsync: GridFS info not found for {fileId}", fileId);
            throw new FileNotFoundException();
        }

        var ms = new MemoryStream();
        await _bucket.DownloadToStreamAsync(oid, ms);
        ms.Position = 0;
        var ct = info.Metadata != null && info.Metadata.Contains("contentType") ? info.Metadata["contentType"].AsString : "application/octet-stream";
        _logger?.LogInformation("Repository DownloadImageAsync returning file {fileId} contentType={ct}", fileId, ct);
        return (ms, ct, info.Filename);
    }

    public async Task<bool> DeleteImageAsync(string sessionId, string imageId)
    {
        _logger?.LogInformation("Repository DeleteImageAsync called for session {sessionId} imageId={imageId}", sessionId, imageId);
        var session = await GetByIdAsync(sessionId);
        if (session is null) return false;

        var found = session.Images?.FirstOrDefault(i => i.Id == imageId);
        if (found is null) return false;

        try
        {
            await _bucket.DeleteAsync(ObjectId.Parse(imageId));
        }
        catch { }

        await _sessions.UpdateOneAsync(s => s.SessionId == sessionId, Builders<Session>.Update.PullFilter(s => s.Images, Builders<ImageRef>.Filter.Eq(i => i.Id, imageId)));

        if (found.IsThumbnail)
        {
            var sessionAfter = await GetByIdAsync(sessionId);
            if (sessionAfter is not null)
            {
                var next = sessionAfter.Images?.FirstOrDefault();
                if (next is not null)
                {
                    var images = sessionAfter.Images ?? new List<ImageRef>();
                    foreach (var im in images)
                    {
                        im.IsThumbnail = im.Id == next.Id;
                    }
                    await _sessions.UpdateOneAsync(Builders<Session>.Filter.Eq(s => s.SessionId, sessionId), Builders<Session>.Update.Set(s => s.Images, images));
                }
            }
        }

        _logger?.LogInformation("Repository DeleteImageAsync removed image {imageId} for session {sessionId}", imageId, sessionId);
        return true;
    }

    public async Task<bool> SetImageThumbnailAsync(string sessionId, string imageId)
    {
        _logger?.LogInformation("Repository SetImageThumbnailAsync called for session {sessionId} imageId={imageId}", sessionId, imageId);
        var session = await GetByIdAsync(sessionId);
        if (session is null) return false;

        var images = session.Images ?? new List<ImageRef>();
        var found = images.FirstOrDefault(i => i.Id == imageId);
        if (found is null) return false;

        // Remove old thumbnail
        foreach (var img in images)
        {
            img.IsThumbnail = img.Id == imageId;
        }

        await _sessions.UpdateOneAsync(Builders<Session>.Filter.Eq(s => s.SessionId, sessionId), Builders<Session>.Update.Set(s => s.Images, images));
        _logger?.LogInformation("Repository SetImageThumbnailAsync set image {imageId} as thumbnail for session {sessionId}", imageId, sessionId);
        return true;
    }
}
