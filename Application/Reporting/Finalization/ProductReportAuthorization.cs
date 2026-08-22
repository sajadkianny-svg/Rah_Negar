using Rah_Negar.Foundation.Application.Security;

namespace Rah_Negar.Foundation.Application.Reporting.Finalization;

/// <summary>Ordinary finalization is authorized by an active ShiftProfile; no management proof is involved.</summary>
public sealed class ShiftProfileReportFinalizationAuthorizer : IReportFinalizationAuthorizer
{
    private readonly Func<string, CancellationToken, Task<ShiftProfile?>> _profiles;

    public ShiftProfileReportFinalizationAuthorizer(
        Func<string, CancellationToken, Task<ShiftProfile?>> profiles) =>
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));

    public async Task<ReportFinalizationAuthorizationResult> AuthorizeAsync(
        ReportFinalizationRequest request,
        ReportFinalizationContext context,
        CancellationToken cancellationToken = default)
    {
        ShiftProfile? profile = await _profiles(context.ActorIdentity, cancellationToken).ConfigureAwait(false);
        return profile is not null &&
               StringComparer.Ordinal.Equals(profile.ShiftProfileId, request.ActorIdentity) &&
               StringComparer.Ordinal.Equals(profile.StationId, request.Projection.Identity.StationId) &&
               OperationalAuthorizationPolicy.IsAuthorized(profile, OperationalAction.FinalizeReport)
            ? ReportFinalizationAuthorizationResult.Authorized()
            : ReportFinalizationAuthorizationResult.Rejected(
                new ReportFinalizationAuthorizationFailure(
                    "report.finalization.shift-profile-required", "An active matching ShiftProfile is required."));
    }
}

public static class ReportReopenAuthorizationPolicy
{
    public static bool IsAuthorized(
        string initiatingShiftProfileId,
        string reportScope,
        ManagementAuthorizationProof? proof,
        DateTimeOffset now) =>
        proof is not null && proof.AppliesTo(
            initiatingShiftProfileId, ProtectedAction.ReopenFinalizedReport, reportScope, now);
}
