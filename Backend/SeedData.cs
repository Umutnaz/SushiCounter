// ...existing code...
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using MongoDB.Bson;

namespace Backend;

public static class SeedData
{
    // Ensures a small set of users and sessions exist if the database is empty.
    // Reads Backend/Passwords.txt (tries several relative locations) for email/password pairs.
    public static async Task EnsureSeedData(IMongoClient client)
    {
        var databaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME")
                           ?? throw new InvalidOperationException("MONGO_DATABASE_NAME is not set.");
        var db = client.GetDatabase(databaseName);

        var usersCol = db.GetCollection<User>("Users");
        var sessionsCol = db.GetCollection<Session>("Sessions");

        var existingUsers = await usersCol.CountDocumentsAsync(FilterDefinition<User>.Empty);
        if (existingUsers > 0) return; // already have data

        // Find Passwords.txt file in a few likely relative spots (bin/Debug/... -> project root)
        string? pwdPath = null;
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Passwords.txt"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Passwords.txt"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "Passwords.txt"),
            Path.Combine(Directory.GetCurrentDirectory(), "Passwords.txt")
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full)) { pwdPath = full; break; }
        }

        List<(string Email, string Password)> entries = new();
        if (pwdPath is not null)
        {
            var lines = await File.ReadAllLinesAsync(pwdPath);
            string? currentPwd = null;
            string? currentEmail = null;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    if (!string.IsNullOrWhiteSpace(currentEmail) && !string.IsNullOrWhiteSpace(currentPwd))
                    {
                        entries.Add((currentEmail, currentPwd));
                    }
                    currentEmail = null; currentPwd = null;
                    continue;
                }

                if (line.StartsWith("Password:", StringComparison.OrdinalIgnoreCase))
                {
                    currentPwd = line.Substring("Password:".Length).Trim();
                }
                else if (line.StartsWith("Email:", StringComparison.OrdinalIgnoreCase))
                {
                    currentEmail = line.Substring("Email:".Length).Trim();
                }
            }
            // final flush
            if (!string.IsNullOrWhiteSpace(currentEmail) && !string.IsNullOrWhiteSpace(currentPwd)) entries.Add((currentEmail, currentPwd));
        }

        // Fallback if none found
        if (entries.Count == 0)
        {
            entries.Add(("alice@example.com", "Password1"));
            entries.Add(("bob@example.com", "Password2"));
            entries.Add(("carol@example.com", "Password3"));
        }

        // Create users (hashing with Argon2)
        var now = DateTime.UtcNow;
        var seedUsers = new List<User>();
        var nameIndex = 1;
        foreach (var (email, password) in entries)
        {
            var emailNorm = email.Trim().ToLowerInvariant();
            var name = emailNorm.Split('@')[0];
            if (string.IsNullOrWhiteSpace(name)) name = "user" + (nameIndex++).ToString();
            // ensure unique-ish name
            name = name + (nameIndex > 1 ? nameIndex.ToString() : string.Empty);

            var user = new User
            {
                Name = name,
                Email = emailNorm,
                Password = Argon2PasswordHasher.Hash(password),
                CreatedAt = now,
                UpdatedAt = now,
                Friends = new List<User>(),
                Sessions = new List<Session>(),
                IncomingRequests = new List<FriendRequest>(),
                OutgoingRequests = new List<FriendRequest>()
            };
            seedUsers.Add(user);
        }

        // Insert users and let Mongo assign ids
        await usersCol.InsertManyAsync(seedUsers);

        // After insert, users in seedUsers should have UserId populated by driver
        // Build shallow friend lists (everyone friends with everyone)
        var shallowUsers = seedUsers.Select(u => new User { UserId = u.UserId, Name = u.Name, Email = u.Email }).ToList();
        foreach (var u in seedUsers)
        {
            var friends = shallowUsers.Where(x => x.UserId != u.UserId).ToList();
            var update = Builders<User>.Update.Set(x => x.Friends, friends);
            await usersCol.UpdateOneAsync(Builders<User>.Filter.Eq(x => x.UserId, u.UserId), update);
        }

        // Create a couple of sessions
        if (seedUsers.Count > 0)
        {
            var sessions = new List<Session>();
            var creator = seedUsers[0];

            var s1 = new Session
            {
                Title = "Sushi Night",
                RestaurantName = "Sushi Place",
                Description = "Weekly sushi meetup",
                CreatorId = creator.UserId,
                CreatedAt = now,
                IsActive = true,
                Participants = seedUsers.Skip(1).Where(u => !string.IsNullOrWhiteSpace(u.UserId)).Select(u => new Participant { UserId = u.UserId!, Count = 0 }).ToList()
            };

            var s2 = new Session
            {
                Title = "Lunch Sushi",
                RestaurantName = "Downtown Sushi",
                Description = "Casual lunch",
                CreatorId = seedUsers.Count > 1 ? seedUsers[1].UserId : creator.UserId,
                CreatedAt = now,
                IsActive = true,
                Participants = seedUsers.Skip(2).Where(u => !string.IsNullOrWhiteSpace(u.UserId)).Select(u => new Participant { UserId = u.UserId!, Count = 0 }).ToList()
            };

            sessions.Add(s1);
            sessions.Add(s2);

            await sessionsCol.InsertManyAsync(sessions);

            // Refresh inserted sessions to obtain their SessionId values (driver should populate but be defensive)
            var firstSession = await sessionsCol.Find(s => s.Title == s1.Title && s.CreatorId == s1.CreatorId).FirstOrDefaultAsync();
            var secondSession = await sessionsCol.Find(s => s.Title == s2.Title && s.CreatorId == s2.CreatorId).FirstOrDefaultAsync();

            // Upload a tiny sample image (1x1 PNG) into GridFS and attach to first session
            try
            {
                var bucket = new GridFSBucket(db);
                // 1x1 transparent PNG
                var pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQYV2NgYGD4DwABBAEAk0nKxQAAAABJRU5ErkJggg==";
                var pngBytes = Convert.FromBase64String(pngBase64);
                using var ms = new MemoryStream(pngBytes);
                var options = new GridFSUploadOptions { Metadata = new BsonDocument { { "contentType", "image/png" }, { "uploadedAt", DateTime.UtcNow } } };
                var id = await bucket.UploadFromStreamAsync("seed-sample.png", ms, options);
                var fileId = id.ToString();

                var imageRef = new ImageRef { Id = fileId, FileName = "seed-sample.png", ContentType = "image/png", UploadedAt = DateTime.UtcNow, UploadedBy = "seed", IsThumbnail = true };

                // Push to first session (use refreshed session id)
                if (firstSession is not null)
                {
                    await sessionsCol.UpdateOneAsync(Builders<Session>.Filter.Eq(s => s.SessionId, firstSession.SessionId), Builders<Session>.Update.Push(s => s.Images, imageRef));
                }
                else if (sessions.Count > 0)
                {
                    // fallback: push to the inserted in-memory session if it has id
                    var memFirst = sessions.First();
                    if (!string.IsNullOrWhiteSpace(memFirst.SessionId))
                        await sessionsCol.UpdateOneAsync(Builders<Session>.Filter.Eq(s => s.SessionId, memFirst.SessionId), Builders<Session>.Update.Push(s => s.Images, imageRef));
                }

                Console.WriteLine($"SeedData: uploaded sample image {fileId} and attached to session");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SeedData: failed to upload sample image: {ex.Message}");
            }
        }

        Console.WriteLine($"SeedData: Inserted {seedUsers.Count} users and sample sessions into '{databaseName}' database.");
    }
}
// ...existing code...

