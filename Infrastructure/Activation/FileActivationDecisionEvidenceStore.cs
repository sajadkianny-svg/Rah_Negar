using System.Text.Json;
using System.Text.Json.Serialization;
using Rah_Negar.Foundation.Application.Activation;

namespace Rah_Negar.Infrastructure.Activation;

/// <summary>
/// Appends non-secret activation eligibility/state evidence to an explicit
/// caller-supplied file. It is intentionally not wired into startup and does
/// not read, discover, replace, or redirect the production database.
/// </summary>
public sealed class FileActivationDecisionEvidenceStore : IActivationDecisionEvidenceStore, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public FileActivationDecisionEvidenceStore(string explicitEvidencePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitEvidencePath);
        if (!Path.IsPathFullyQualified(explicitEvidencePath))
            throw new ArgumentException("Evidence path must be fully qualified.", nameof(explicitEvidencePath));
        _path = Path.GetFullPath(explicitEvidencePath);
        string? directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Evidence path must have a directory.", nameof(explicitEvidencePath));
        Directory.CreateDirectory(directory);
    }

    public async Task<bool> TryAppendAsync(
        ProductionActivationEligibilityReceipt receipt,
        ActivationAuditEntry auditEntry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(auditEntry);
        int auditSuffixIndex = auditEntry.AuditEntryId.LastIndexOf(":audit",
            StringComparison.Ordinal);
        if (!ActivationAuditEntryValidator.IsSafeAndComplete(auditEntry) ||
            auditSuffixIndex <= 0 ||
            !StringComparer.Ordinal.Equals(receipt.ReceiptId,
                auditEntry.AuditEntryId[..auditSuffixIndex]))
            return false;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write,
                FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await JsonSerializer.SerializeAsync(stream, new ActivationDecisionEvidenceLine(receipt, auditEntry),
                JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gate.Dispose();
    }

    private sealed record ActivationDecisionEvidenceLine(
        ProductionActivationEligibilityReceipt Receipt,
        ActivationAuditEntry AuditEntry);
}
