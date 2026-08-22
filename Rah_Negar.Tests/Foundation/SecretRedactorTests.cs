using Rah_Negar.Infrastructure.Foundation.Logging;

namespace Rah_Negar.Tests.Foundation;

public sealed class SecretRedactorTests
{
    [Fact]
    public void Redact_replaces_secret_values_and_preserves_safe_values()
    {
        var input = new Dictionary<string, object?>
        {
            ["PersonnelNo"] = "P-101",
            ["Password"] = "plain-text",
            ["PasswordHash"] = "hash-value",
            ["RecoveryCode"] = "recovery-value",
            ["CorrelationId"] = "c-1"
        };

        var redactor = new SecretRedactor();
        IReadOnlyDictionary<string, object?> result = redactor.Redact(input);

        Assert.Equal("P-101", result["PersonnelNo"]);
        Assert.Equal("c-1", result["CorrelationId"]);
        Assert.Equal(SecretRedactor.RedactedValue, result["Password"]);
        Assert.Equal(SecretRedactor.RedactedValue, result["PasswordHash"]);
        Assert.Equal(SecretRedactor.RedactedValue, result["RecoveryCode"]);
        Assert.Equal("plain-text", input["Password"]);
    }

    [Fact]
    public void Redact_matches_sensitive_keys_case_insensitively()
    {
        var redactor = new SecretRedactor();
        var input = new Dictionary<string, object?> { ["credentialVersionToken"] = "value" };

        IReadOnlyDictionary<string, object?> result = redactor.Redact(input);

        Assert.Equal(SecretRedactor.RedactedValue, result["credentialVersionToken"]);
    }
}
