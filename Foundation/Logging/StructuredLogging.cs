namespace Rah_Negar.Foundation.Logging;

public enum FoundationLogLevel
{
    Information,
    Warning,
    Error,
    Critical
}

public interface IStructuredLogger
{
    void Write(
        FoundationLogLevel level,
        string eventCode,
        string message,
        IReadOnlyDictionary<string, object?>? properties = null,
        Exception? exception = null);
}

public interface ISecretRedactor
{
    IReadOnlyDictionary<string, object?> Redact(
        IReadOnlyDictionary<string, object?> properties);
}
