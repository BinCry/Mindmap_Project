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
        // 1. Kiểm tra an toàn đầu vào
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt))
        {
            return false;
        }

        try
        {
            // 2. Cố gắng giải mã Base64 (Đây là đoạn hay gây lỗi Crash nếu data sai)
            var saltBytes = Convert.FromBase64String(salt);
            var storedHashBytes = Convert.FromBase64String(hash);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA512);
            var keyBytes = pbkdf2.GetBytes(KeySize);

            // 3. So sánh hash mới tạo với hash trong DB
            return CryptographicOperations.FixedTimeEquals(keyBytes, storedHashBytes);
        }
        catch (FormatException)
        {
            // 🔥 Bắt được lỗi Base64 sai định dạng -> Coi như sai pass, không Crash app
            System.Diagnostics.Debug.WriteLine("Lỗi định dạng Hash/Salt trong Database.");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi Verify Password: {ex.Message}");
            return false;
        }
    }
}