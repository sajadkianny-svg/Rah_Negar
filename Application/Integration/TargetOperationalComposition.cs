using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Security;

namespace Rah_Negar.Foundation.Application.Integration;

public enum TargetOperationalRouteArea
{
    Authentication,
    MainData,
    Events,
    Runtime,
    Reporting,
    Security,
    ReportExport
}

public enum TargetOperationalRouteAccess
{
    Read,
    Write,
    ProtectedWrite
}

public sealed record TargetOperationalRouteDescriptor(
    string RouteId,
    TargetOperationalRouteArea Area,
    TargetOperationalRouteAccess Access,
    bool IsComposed,
    bool IsEnabled,
    bool ProductionMutationAllowed,
    string LegacyOwner,
    string TargetOwner);

/// <summary>
/// Complete target route inventory for qualification. The route descriptors are composed, but
/// this boundary has no activation or startup registration and remains Legacy-authoritative.
/// </summary>
public sealed class InactiveTargetOperationalComposition
{
    public InactiveTargetOperationalComposition(InactiveTargetSecurityComposition security)
    {
        Security = security ?? throw new ArgumentNullException(nameof(security));
        Routes = TargetOperationalRouteCatalog.Create();
    }

    public TargetSecurityCompositionDescriptor Descriptor => TargetSecurityCompositionDescriptor.Inactive;
    public InactiveTargetSecurityComposition Security { get; }
    public IReadOnlyList<TargetOperationalRouteDescriptor> Routes { get; }
    public bool TargetRoutesEnabled => false;
    public bool LegacyRemainsAuthoritative => true;
    public bool ProductionMutationAllowed => false;
    public bool PreparationOperatorReachable => false;
}

public static class TargetOperationalRouteCatalog
{
    public static ReadOnlyCollection<TargetOperationalRouteDescriptor> Create() => new([
            Route("authentication.read", TargetOperationalRouteArea.Authentication, TargetOperationalRouteAccess.Read, "Legacy login/session", "ShiftProfile session"),
            Route("main-data.read", TargetOperationalRouteArea.MainData, TargetOperationalRouteAccess.Read, "Legacy main data", "Target main data"),
            Route("main-data.write", TargetOperationalRouteArea.MainData, TargetOperationalRouteAccess.Write, "Legacy data entry", "ShiftProfile main-data writer"),
            Route("events.read", TargetOperationalRouteArea.Events, TargetOperationalRouteAccess.Read, "Legacy event tables", "Target Events reader"),
            Route("events.write", TargetOperationalRouteArea.Events, TargetOperationalRouteAccess.Write, "Legacy event entry", "ShiftProfile event writer"),
            Route("runtime.read", TargetOperationalRouteArea.Runtime, TargetOperationalRouteAccess.Read, "Legacy runtime calculation", "Target runtime projection"),
            Route("reports.read", TargetOperationalRouteArea.Reporting, TargetOperationalRouteAccess.Read, "Legacy reports", "Finalized snapshot/report reader"),
            Route("reports.write", TargetOperationalRouteArea.Reporting, TargetOperationalRouteAccess.Write, "Legacy finalization", "Immutable snapshot finalizer"),
            Route("report-export.read", TargetOperationalRouteArea.ReportExport, TargetOperationalRouteAccess.Read, "Legacy report export", "Snapshot export"),
            Route("security.read", TargetOperationalRouteArea.Security, TargetOperationalRouteAccess.Read, "Legacy settings/session", "Target security persistence"),
            Route("security.protected-write", TargetOperationalRouteArea.Security, TargetOperationalRouteAccess.ProtectedWrite, "Legacy protected settings", "ManagementCredential protected action")
    ]);

    private static TargetOperationalRouteDescriptor Route(string id, TargetOperationalRouteArea area,
        TargetOperationalRouteAccess access, string legacyOwner, string targetOwner) =>
        new(id, area, access, IsComposed: true, IsEnabled: false,
            ProductionMutationAllowed: false, legacyOwner, targetOwner);
}
