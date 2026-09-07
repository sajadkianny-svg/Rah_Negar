using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Rah_Negar.Services;

/// <summary>
/// Authenticated, versioned encryption for offline backup files.
/// The encryption key is supplied by OS-protected key custody; it is never embedded
/// in the application or stored in the backup itself.
/// </summary>
public static class BackupEncryptionService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RNBK");
    private const byte CurrentVersion = 2;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static void EncryptFile(string sourcePath, string destinationPath) =>
        EncryptFile(sourcePath, destinationPath, WindowsProtectedBackupKeyCustody.Instance);

    internal static void EncryptFile(string sourcePath, string destinationPath, IBackupKeyCustody custody)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(custody);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("فایل مبدا برای بک اپ پیدا نشد", sourcePath);

        byte[] key = custody.LoadOrCreateKey();
        try
        {
            if (key.Length != KeySize)
                throw new CryptographicException("Backup key custody returned an invalid key.");

            byte[] plaintext = File.ReadAllBytes(sourcePath);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[TagSize];
            byte[] header = [.. Magic, CurrentVersion];

            using (AesGcm aes = new(key, TagSize))
                aes.Encrypt(nonce, plaintext, ciphertext, tag, header);

            string fullDestination = Path.GetFullPath(destinationPath);
            string directory = Path.GetDirectoryName(fullDestination)
                ?? throw new InvalidOperationException("مسیر خروجی معتبر نیست");
            Directory.CreateDirectory(directory);
            string temporary = fullDestination + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(header);
                    output.Write(nonce);
                    output.Write(tag);
                    output.Write(ciphertext);
                    output.Flush(flushToDisk: true);
                }
                ReplaceAtomically(temporary, fullDestination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public static void DecryptFile(string encryptedPath, string destinationPath) =>
        DecryptFile(encryptedPath, destinationPath, WindowsProtectedBackupKeyCustody.Instance);

    internal static void DecryptFile(string encryptedPath, string destinationPath, IBackupKeyCustody custody)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(custody);
        if (!File.Exists(encryptedPath))
            throw new FileNotFoundException("فایل بک اپ پیدا نشد", encryptedPath);

        byte[] file = File.ReadAllBytes(encryptedPath);
        int minimum = Magic.Length + 1 + NonceSize + TagSize;
        if (file.Length < minimum || !file.AsSpan(0, Magic.Length).SequenceEqual(Magic) ||
            file[Magic.Length] != CurrentVersion)
            throw new InvalidDataException("نسخه فایل بک اپ پشتیبانی نمی‌شود");

        byte[] header = file[..(Magic.Length + 1)];
        byte[] nonce = file[(Magic.Length + 1)..(Magic.Length + 1 + NonceSize)];
        byte[] tag = file[(Magic.Length + 1 + NonceSize)..minimum];
        byte[] ciphertext = file[minimum..];
        byte[] plaintext = new byte[ciphertext.Length];
        byte[] key = custody.LoadOrCreateKey();
        try
        {
            if (key.Length != KeySize)
                throw new CryptographicException("Backup key custody returned an invalid key.");
            using (AesGcm aes = new(key, TagSize))
                aes.Decrypt(nonce, ciphertext, tag, plaintext, header);

            string fullDestination = Path.GetFullPath(destinationPath);
            string directory = Path.GetDirectoryName(fullDestination)
                ?? throw new InvalidOperationException("مسیر خروجی معتبر نیست");
            Directory.CreateDirectory(directory);
            string temporary = fullDestination + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(plaintext);
                    output.Flush(flushToDisk: true);
                }
                ReplaceAtomically(temporary, fullDestination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new InvalidDataException("فایل بک اپ دستکاری شده یا کلید آن معتبر نیست", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static void ReplaceAtomically(string temporary, string destination)
    {
        if (!File.Exists(destination))
        {
            File.Move(temporary, destination);
            return;
        }
        File.Replace(temporary, destination, null, ignoreMetadataErrors: true);
    }
}

internal interface IBackupKeyCustody
{
    byte[] LoadOrCreateKey();
}

/// <summary>Windows DPAPI protects the randomly generated key at rest for the current operator.</summary>
internal sealed class WindowsProtectedBackupKeyCustody : IBackupKeyCustody
{
    public static WindowsProtectedBackupKeyCustody Instance { get; } = new();
    private const int CryptprotectUiForbidden = 0x1;
    private readonly object _sync = new();

    private WindowsProtectedBackupKeyCustody() { }

    public byte[] LoadOrCreateKey()
    {
        lock (_sync)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RahNegar", "backup.key.dpapi");
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("مسیر امن کلید بک اپ مشخص نیست");
            Directory.CreateDirectory(directory);
            if (File.Exists(path))
                return Dpapi.Unprotect(File.ReadAllBytes(path));

            byte[] key = RandomNumberGenerator.GetBytes(32);
            byte[] protectedKey = Dpapi.Protect(key);
            try
            {
                File.WriteAllBytes(path, protectedKey);
                return key;
            }
            finally { CryptographicOperations.ZeroMemory(protectedKey); }
        }
    }

    private static class Dpapi
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Blob { public int Length; public IntPtr Data; }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref Blob dataIn, string? description,
            IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob dataOut);

        [DllImport("crypt32.dll", SetLastError = true)]
        private static extern bool CryptUnprotectData(ref Blob dataIn, IntPtr description,
            IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr handle);

        public static byte[] Protect(byte[] input) => Transform(input, protect: true);
        public static byte[] Unprotect(byte[] input) => Transform(input, protect: false);

        private static byte[] Transform(byte[] input, bool protect)
        {
            IntPtr inputMemory = Marshal.AllocHGlobal(input.Length);
            try
            {
                Marshal.Copy(input, 0, inputMemory, input.Length);
                Blob source = new() { Length = input.Length, Data = inputMemory };
                Blob result = default;
                bool ok = protect
                    ? CryptProtectData(ref source, "RahNegar backup key", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        CryptprotectUiForbidden, ref result)
                    : CryptUnprotectData(ref source, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref result);
                if (!ok) throw new CryptographicException(Marshal.GetLastWin32Error());
                byte[] output = new byte[result.Length];
                Marshal.Copy(result.Data, output, 0, result.Length);
                LocalFree(result.Data);
                return output;
            }
            finally { Marshal.FreeHGlobal(inputMemory); }
        }
    }
}
