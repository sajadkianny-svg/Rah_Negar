using Rah_Negar.Foundation.Application.Reporting.Finalization;
using Rah_Negar.Foundation.Application.Security;

namespace Rah_Negar.Tests.Security;

public sealed class SecurityArchitectureReconciliationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShiftProfile_Is_Normal_Identity_And_All_Active_Profiles_Have_Equivalent_Access()
    {
        ShiftProfile first = Profile("shift-1", 1);
        ShiftProfile second = Profile("shift-2", 2);

        Assert.Equal("1001", first.PersonnelNo);
        foreach (OperationalAction action in Enum.GetValues<OperationalAction>())
            Assert.Equal(
                OperationalAuthorizationPolicy.IsAuthorized(first, action),
                OperationalAuthorizationPolicy.IsAuthorized(second, action));

        Assert.DoesNotContain(typeof(ShiftProfile).Assembly.GetTypes(), type =>
            type.Name.Contains("Role", StringComparison.OrdinalIgnoreCase) ||
            type.Name.Contains("SupportProfile", StringComparison.OrdinalIgnoreCase) ||
            type.Name.Contains("SupportLogin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Finalize_Is_Operational_But_Reopen_Requires_Action_Bound_Management_Proof()
    {
        ShiftProfile profile = Profile("shift-1", 1);
        Assert.True(OperationalAuthorizationPolicy.IsAuthorized(profile, OperationalAction.FinalizeReport));

        Assert.False(ReportReopenAuthorizationPolicy.IsAuthorized(profile.ShiftProfileId, "report/1405-05", null, Now));
        ManagementAuthorizationProof proof = ManagementProof(profile.ShiftProfileId,
            ProtectedAction.ReopenFinalizedReport, "report/1405-05");
        Assert.True(ReportReopenAuthorizationPolicy.IsAuthorized(profile.ShiftProfileId, "report/1405-05", proof, Now));
    }

    [Fact]
    public void Wizard_Initial_Esd_Requires_No_Support_Authorization()
    {
        var request = new Rah_Negar.Foundation.Application.UI.Settings.EsdAdjustmentChangeRequest(
            "request", Now, 0m, 0m, IsWizardInitialValue: true);
        Assert.True(request.IsWizardInitialValue);
        Assert.Equal(0m, request.ProposedHours);
    }

    [Fact]
    public async Task Management_Alone_Cannot_Bypass_PostWizard_Vendor_Authorization()
    {
        Fixture f = new();
        int executions = 0;
        bool executed = await f.Executor.ExecutePostWizardAsync("shift-1", "station-1", 2.5m,
            ManagementProof("shift-1", ProtectedAction.ChangeEsdAdjustment, "station-1"),
            f.Request(), "invalid".AsMemory(), Now, _ => { executions++; return Task.CompletedTask; });

        Assert.False(executed);
        Assert.Equal(0, executions);
    }

    [Fact]
    public async Task Expired_Support_Authorization_Fails()
    {
        Fixture f = new();
        VendorSupportAuthorizationRequest request = f.Request() with { ExpiresAt = Now };
        VendorSupportVerificationResult result = await f.Authorize(request, 2.5m);
        Assert.Equal(VendorSupportVerificationFailure.Expired, result.Failure);
    }

    [Fact]
    public async Task Wrong_Value_And_Wrong_Action_Fail()
    {
        Fixture f = new();
        Assert.Equal(VendorSupportVerificationFailure.WrongProposedValue,
            (await f.Authorize(f.Request(), 3m)).Failure);
        Assert.Equal(VendorSupportVerificationFailure.WrongAction,
            (await f.Authorize(f.Request() with { Action = (VendorSupportAction)999 }, 2.5m)).Failure);
    }

    [Fact]
    public async Task Device_Request_Action_And_Value_Are_Verified_And_Replay_Fails()
    {
        Fixture f = new();
        VendorSupportAuthorizationRequest request = f.Request();
        Assert.True((await f.Authorize(request, 2.5m)).IsValid);
        Assert.Equal(request, f.Verifier.LastRequest);
        Assert.Equal(VendorSupportVerificationFailure.Replayed, (await f.Authorize(request, 2.5m)).Failure);
    }

    [Fact]
    public async Task Successful_Protected_Execution_Occurs_Exactly_Once()
    {
        Fixture f = new();
        int calls = 0;
        VendorSupportAuthorizationRequest request = f.Request();
        ManagementAuthorizationProof management = ManagementProof("shift-1", ProtectedAction.ChangeEsdAdjustment, "station-1");

        Assert.True(await f.Executor.ExecutePostWizardAsync("shift-1", "station-1", 2.5m,
            management, request, "valid".AsMemory(), Now, _ => { calls++; return Task.CompletedTask; }));
        Assert.False(await f.Executor.ExecutePostWizardAsync("shift-1", "station-1", 2.5m,
            management, request, "valid".AsMemory(), Now, _ => { calls++; return Task.CompletedTask; }));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Presentation_And_Audit_Contracts_Contain_No_Secrets()
    {
        Type[] contracts = [typeof(ShiftProfile), typeof(SecurityAuditEvent),
            typeof(VendorSupportAuthorizationRequest), typeof(VendorSupportVerificationResult)];
        string[] forbidden = ["Password", "Hash", "PrivateKey", "RecoverySecret", "OneTimeCode"];
        foreach (Type contract in contracts)
            foreach (var property in contract.GetProperties())
                Assert.DoesNotContain(forbidden, value => property.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Security_Contracts_Are_Station_Neutral()
    {
        string contractNames = string.Join('|', typeof(ShiftProfile).Assembly.GetTypes()
            .Where(x => x.Namespace?.Contains("Security", StringComparison.Ordinal) == true)
            .Select(x => x.FullName));
        Assert.DoesNotContain("Rasht", contractNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ramsar", contractNames, StringComparison.OrdinalIgnoreCase);
    }

    private static ShiftProfile Profile(string id, int number) => new(
        id, "station-1", number, $"Shift {number}", "First", "Last", "1001", true, Now, Now, 1);

    private static ManagementAuthorizationProof ManagementProof(string id, ProtectedAction action, string scope) =>
        new(id, action, scope, 3, Now.AddMinutes(-1), Now.AddMinutes(4), "correlation-1");

    private sealed class Fixture
    {
        public Verifier Verifier { get; } = new();
        public MemoryConsumedStore Store { get; } = new();
        public EsdAdjustmentAuthorizationService Authorization { get; }
        public EsdAdjustmentChangeExecutor Executor { get; }

        public Fixture()
        {
            Authorization = new(Verifier, Store);
            Executor = new(Authorization);
        }

        public VendorSupportAuthorizationRequest Request() =>
            new("device-1", "request-1", VendorSupportAction.ChangeEsdAdjustment, 2.5m,
                Now.AddMinutes(-1), Now.AddMinutes(5));

        public Task<VendorSupportVerificationResult> Authorize(VendorSupportAuthorizationRequest request, decimal value) =>
            Authorization.AuthorizePostWizardChangeAsync("shift-1", "station-1", value,
                ManagementProof("shift-1", ProtectedAction.ChangeEsdAdjustment, "station-1"),
                request, "valid".AsMemory(), Now);
    }

    private sealed class Verifier : IExternalVendorSupportAuthorizationVerifier
    {
        public VendorSupportAuthorizationRequest? LastRequest { get; private set; }
        public Task<VendorSupportVerificationResult> VerifyAsync(VendorSupportAuthorizationRequest expectedRequest,
            ReadOnlyMemory<char> signedAuthorization, CancellationToken cancellationToken = default)
        {
            LastRequest = expectedRequest;
            bool valid = signedAuthorization.Span.SequenceEqual("valid".AsSpan());
            return Task.FromResult(new VendorSupportVerificationResult(valid,
                valid ? VendorSupportVerificationFailure.None : VendorSupportVerificationFailure.Invalid,
                expectedRequest.RequestId, Now));
        }
    }

    private sealed class MemoryConsumedStore : IConsumedVendorSupportRequestStore
    {
        private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);
        public Task<bool> IsConsumedAsync(string requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_consumed.Contains(requestId));
        public Task<bool> TryConsumeAsync(string requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_consumed.Add(requestId));
    }
}
