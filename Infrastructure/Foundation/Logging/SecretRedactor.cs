using Rah_Negar.Foundation.Logging;

namespace Rah_Negar.Infrastructure.Foundation.Logging;

public sealed class SecretRedactor : ISecretRedactor
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly string[] SensitiveFragments =
    {
        "password", "hash", "salt", "secret", "token", "credential", "recovery"
    };

    public IReadOnlyDictionary<string, object?> Redact(
        IReadOnlyDictionary<string, object?> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        return properties.ToDictionary(
            pair => pair.Key,
            pair => IsSensitive(pair.Key) ? RedactedValue : pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSensitive(string key) =>
        SensitiveFragments.Any(fragment =>
            key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
