namespace Rah_Negar.Foundation.Identity;

public interface IManagementAuthorizationContext
{
    bool IsAuthorized { get; }
    string ActionCode { get; }
    Guid InitiatingShiftProfileId { get; }
    DateTimeOffset AuthorizedAtUtc { get; }
    DateTimeOffset ExpiresAtUtc { get; }
    long CredentialVersion { get; }
    string CorrelationId { get; }
}
