using Rah_Negar.Foundation.Application.Authority;
using Rah_Negar.Foundation.Application.Integration;

namespace Rah_Negar.Tests.Integration;

public sealed class Batch2InactiveTargetTests
{
    [Fact]
    public void Current_authority_is_legacy_only_and_target_routes_are_disabled()
    {
        AuthorityStateRecord state = AuthorityStateRecord.Legacy("production", "all");
        InactiveTargetOperationalWriteBoundary fence = new();
        Assert.True(AuthorityRoutingGuard.IsLegacyOperationalRoutingAllowed(state));
        Assert.False(AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(state));
        Assert.False(fence.IsEnabled);
        Assert.False(fence.IsReachableFromLegacyOperation);
        Assert.False(InactiveTargetOperationalWriteBoundary.CanWrite(state));
        Assert.Throws<InvalidOperationException>(fence.EnsureWriteIsDisabled);
    }

    [Fact]
    public void Malformed_authority_cannot_enable_target_writes()
    {
        AuthorityStateRecord malformed = AuthorityStateRecord.Legacy() with { TargetRoutingEnabled = true };
        Assert.False(InactiveTargetOperationalWriteBoundary.CanWrite(malformed));
        Assert.False(AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(malformed));
    }
}
