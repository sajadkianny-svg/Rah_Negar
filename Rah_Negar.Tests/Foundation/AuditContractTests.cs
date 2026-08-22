using Rah_Negar.Foundation.Audit;

namespace Rah_Negar.Tests.Foundation;

public sealed class AuditContractTests
{
    [Fact]
    public void Entry_retains_stable_actor_and_non_secret_context()
    {
        Guid shiftProfileId = Guid.NewGuid();
        var actor = new AuditActor(shiftProfileId, "P-101", "Supervisor", "ShiftProfile");
        var entry = new SystemAuditEntry(
            Guid.NewGuid(),
            "SETTINGS_VIEWED",
            DateTimeOffset.UtcNow,
            actor,
            "Success",
            "correlation-1",
            "Settings",
            "Application",
            ManagementAuthorizationSupplied: true,
            Reason: "Verification");

        Assert.Equal(shiftProfileId, entry.Actor.ShiftProfileId);
        Assert.Equal("P-101", entry.Actor.PersonnelNoSnapshot);
        Assert.True(entry.ManagementAuthorizationSupplied);
        Assert.Equal("correlation-1", entry.CorrelationId);
    }
}
