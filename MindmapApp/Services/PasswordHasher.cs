using System;
using System.Security.Cryptography;

namespace MindmapApp.Services;

public class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA512);
        var keyBytes = pbkdf2.GetBytes(KeySize);
        return (Convert.ToBase64String(keyBytes), Convert.ToBase64String(saltBytes));
    }

    public bool Verify(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA512);
        var keyBytes = pbkdf2.GetBytes(KeySize);
        return CryptographicOperations.FixedTimeEquals(keyBytes, Convert.FromBase64String(hash));
    }
}
