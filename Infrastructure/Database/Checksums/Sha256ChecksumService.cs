using System.Security.Cryptography;
using System.Text;

namespace Rah_Negar.Infrastructure.Database.Checksums;

public sealed class Sha256ChecksumService : IChecksumService
{
    public string Compute(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }

    public async Task<string> ComputeFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
