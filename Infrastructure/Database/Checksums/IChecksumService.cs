namespace Rah_Negar.Infrastructure.Database.Checksums;

public interface IChecksumService
{
    string Compute(string content);
    Task<string> ComputeFileAsync(string path, CancellationToken cancellationToken = default);
}
