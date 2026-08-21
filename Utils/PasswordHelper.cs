using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



using System.Security.Cryptography;

namespace Rah_Negar.Utils;

/// <summary>
/// ابزار ساخت و بررسی رمز عبور
/// </summary>
public static class PasswordHelper
{
    /// <summary>
    /// تولید Salt تصادفی برای هش رمز
    /// </summary>
    public static string CreateSalt()
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(salt);
    }

    /// <summary>
    /// تولید هش رمز بر اساس Salt
    /// </summary>
    public static string HashPassword(string password, string saltBase64)
    {
        byte[] salt = Convert.FromBase64String(saltBase64);

        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            100_000,
            HashAlgorithmName.SHA256);

        return Convert.ToBase64String(pbkdf2.GetBytes(32));
    }

    /// <summary>
    /// بررسی صحت رمز وارد شده با هش ذخیره شده
    /// </summary>
    public static bool VerifyPassword(string password, string saltBase64, string expectedHash)
    {
        string actualHash = HashPassword(password, saltBase64);
        return actualHash == expectedHash;
    }
}