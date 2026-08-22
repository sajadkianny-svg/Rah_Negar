namespace Rah_Negar.Core.Reporting.Snapshot;

public enum SnapshotChecksumState { Pending, Calculated }

public sealed class SnapshotChecksum
{
    public SnapshotChecksum(string algorithm, string integrityFormatVersion,
        SnapshotChecksumState state, string? value = null, long? canonicalPayloadLength = null)
    {
        Algorithm = Required(algorithm, nameof(algorithm));
        IntegrityFormatVersion = Required(integrityFormatVersion, nameof(integrityFormatVersion));
        if (state == SnapshotChecksumState.Calculated &&
            (string.IsNullOrWhiteSpace(value) || canonicalPayloadLength is null or < 0))
            throw new ArgumentException("A calculated checksum requires a value and payload length.");
        if (state == SnapshotChecksumState.Pending && (value is not null || canonicalPayloadLength is not null))
            throw new ArgumentException("Pending checksum metadata cannot contain calculated values.");
        State = state;
        Value = value;
        CanonicalPayloadLength = canonicalPayloadLength;
    }

    public string Algorithm { get; }
    public string IntegrityFormatVersion { get; }
    public SnapshotChecksumState State { get; }
    public string? Value { get; }
    public long? CanonicalPayloadLength { get; }

    public static SnapshotChecksum Pending(string integrityFormatVersion) =>
        new("SHA-256", integrityFormatVersion, SnapshotChecksumState.Pending);

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value.Trim();
}
