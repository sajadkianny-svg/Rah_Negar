using System.Security.Cryptography;
using System.Text;

namespace Rah_Negar.Services;

/// <summary>
/// سرویس رمزنگاری و رمزگشایی فایل‌های Backup برنامه.
/// کاربر برای فایل Backup رمز جداگانه وارد نمی‌کند؛
/// رمزنگاری با کلید داخلی برنامه انجام می‌شود.
/// </summary>
public static class BackupEncryptionService
{
    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    /// <summary>
    /// کلید داخلی برنامه برای رمزنگاری Backup.
    /// این کلید برای جلوگیری از باز شدن مستقیم فایل Backup استفاده می‌شود.
    /// </summary>
    private const string InternalBackupKey =
        "RahNegar.Internal.Backup.Key.2026.V1";

    /// <summary>
    /// فایل دیتابیس را رمزنگاری کرده و به صورت فایل Backup ذخیره می‌کند.
    /// </summary>
    public static void EncryptFile(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("فایل مبدا برای بک اپ پیدا نشد", sourcePath);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] iv = RandomNumberGenerator.GetBytes(IvSize);
        byte[] key = DeriveKey(salt);

        using FileStream inputStream = new(
             sourcePath,
             FileMode.Open,
             FileAccess.Read,
             FileShare.ReadWrite | FileShare.Delete);
        using FileStream outputStream = File.Create(destinationPath);

        outputStream.Write(salt, 0, salt.Length);
        outputStream.Write(iv, 0, iv.Length);

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using CryptoStream cryptoStream = new(
            outputStream,
            aes.CreateEncryptor(),
            CryptoStreamMode.Write);

        inputStream.CopyTo(cryptoStream);
    }

    /// <summary>
    /// فایل Backup رمزنگاری‌شده را رمزگشایی کرده و در مسیر مقصد ذخیره می‌کند.
    /// </summary>
    public static void DecryptFile(string encryptedPath, string destinationPath)
    {
        if (!File.Exists(encryptedPath))
            throw new FileNotFoundException("فایل بک اپ پیدا نشد", encryptedPath);

        using FileStream inputStream = File.OpenRead(encryptedPath);

        byte[] salt = new byte[SaltSize];
        byte[] iv = new byte[IvSize];

        if (inputStream.Read(salt, 0, SaltSize) != SaltSize)
            throw new InvalidDataException("فایل بک اپ معتبر نیست");

        if (inputStream.Read(iv, 0, IvSize) != IvSize)
            throw new InvalidDataException("فایل بک اپ معتبر نیست");

        byte[] key = DeriveKey(salt);

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using CryptoStream cryptoStream = new(
            inputStream,
            aes.CreateDecryptor(),
            CryptoStreamMode.Read);

        using FileStream outputStream = File.Create(destinationPath);

        cryptoStream.CopyTo(outputStream);
    }

    /// <summary>
    /// تولید کلید AES از کلید داخلی برنامه و Salt.
    /// </summary>
    private static byte[] DeriveKey(byte[] salt)
    {
        using Rfc2898DeriveBytes pbkdf2 = new(
            InternalBackupKey,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(KeySize);
    }
}