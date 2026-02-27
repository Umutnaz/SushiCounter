using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Backend;

public static class Argon2PasswordHasher
{
    // Starter-værdier (ok til de fleste projekter)
    private const int SaltSize = 16;          // 128-bit
    private const int HashSize = 32;          // 256-bit
    private const int Iterations = 3;         // t
    private const int MemoryKb = 64 * 1024;   // m = 64 MB
    private const int Parallelism = 1;        // p

    // Returnerer en string du kan gemme i MongoDB
    // Format: $argon2id$m=65536,t=3,p=1$<saltB64>$<hashB64>
    public static string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password må ikke være tomt.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            Iterations = Iterations,
            MemorySize = MemoryKb,
            DegreeOfParallelism = Parallelism
        };

        var hash = argon2.GetBytes(HashSize);

        return $"$argon2id$m={MemoryKb},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string encoded)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(encoded))
            return false;

        // $argon2id$m=...,t=...,p=...$salt$hash
        var parts = encoded.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return false;
        if (parts[0] != "argon2id") return false;

        if (!TryParseParams(parts[1], out var mKb, out var t, out var p))
            return false;

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            Iterations = t,
            MemorySize = mKb,
            DegreeOfParallelism = p
        };

        var actualHash = argon2.GetBytes(expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static bool TryParseParams(string s, out int mKb, out int t, out int p)
    {
        mKb = 0; t = 0; p = 0;

        // "m=65536,t=3,p=1"
        var items = s.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in items)
        {
            var kv = item.Split('=', 2);
            if (kv.Length != 2) return false;

            if (kv[0] == "m" && int.TryParse(kv[1], out var mv)) mKb = mv;
            else if (kv[0] == "t" && int.TryParse(kv[1], out var tv)) t = tv;
            else if (kv[0] == "p" && int.TryParse(kv[1], out var pv)) p = pv;
            else return false;
        }

        return mKb > 0 && t > 0 && p > 0;
    }
}