using Rah_Negar.Foundation.Application.UI.Settings;

namespace Rah_Negar.Tests.UI;

public sealed class ProtectedSettingsUiCoordinatorTests
{
    [Fact]
    public async Task View_Presents_Normal_Protected_Settings()
    {
        Fixture f = new();
        await f.Coordinator.ViewAsync();
        Assert.Equal([ProtectedSettingsViewStatus.Loading, ProtectedSettingsViewStatus.Ready], f.Presenter.States.Select(x => x.Status));
        Assert.Equal("Rasht", f.Presenter.Last.Settings!.StationId);
        Assert.Equal(0m, f.Presenter.Last.Settings.EsdAdjustmentHours);
    }

    [Fact]
    public async Task Denied_Protected_Change_Does_Not_Execute()
    {
        Fixture f = new() { AuthorizationDecision = new(SecureOperationPresentationResult.Denied) };
        int calls = 0;
        await f.Coordinator.RequestSecuritySettingChangeAsync(f.SecurityRequest(), _ => { calls++; return Task.CompletedTask; });
        Assert.Equal(0, calls);
        Assert.Equal(ProtectedSettingsViewStatus.Denied, f.Presenter.Last.Status);
    }

    [Fact]
    public async Task Esd_Zero_Default_Is_Valid_And_Does_Not_Prompt()
    {
        Fixture f = new();
        int calls = 0;
        await f.Coordinator.RequestEsdAdjustmentChangeAsync(f.EsdRequest(0m, 0m), _ => { calls++; return Task.CompletedTask; });
        Assert.Equal(1, calls);
        Assert.Equal(0, f.Authorization.AuthorizeCalls);
        Assert.Equal(SecureOperationPresentationResult.Authorized, f.Audit.Items.Single().Decision);
    }

    [Fact]
    public async Task Wizard_Initial_Esd_Value_Does_Not_Require_Post_Wizard_Authorization()
    {
        Fixture f = new();
        int calls = 0;
        var request = f.EsdRequest(0m, 2.5m) with { IsWizardInitialValue = true };
        await f.Coordinator.RequestEsdAdjustmentChangeAsync(request, _ => { calls++; return Task.CompletedTask; });
        Assert.Equal(1, calls);
        Assert.Equal(0, f.Authorization.AuthorizeCalls);
    }

    [Fact]
    public async Task PostWizard_Esd_Cannot_Bypass_Vendor_Support_With_Direct_Authorization()
    {
        Fixture f = new() { AuthorizationDecision = new(SecureOperationPresentationResult.Authorized) };
        int calls = 0;
        await f.Coordinator.RequestEsdAdjustmentChangeAsync(f.EsdRequest(0m, 1.25m), _ => { calls++; return Task.CompletedTask; });
        Assert.Equal(0, calls);
        Assert.Equal(1, f.Authorization.AuthorizeCalls);
    }

    [Fact]
    public async Task Management_Prompt_Can_Resume_And_Executes_Exactly_Once()
    {
        Fixture f = new()
        {
            AuthorizationDecision = new(SecureOperationPresentationResult.ManagementAuthorizationRequired),
            ManagementDecision = new(SecureOperationPresentationResult.Authorized)
        };
        int calls = 0;
        SecuritySettingChangeRequest request = f.SecurityRequest();
        await f.Coordinator.RequestSecuritySettingChangeAsync(request, _ => { calls++; return Task.CompletedTask; });
        Assert.Equal(ProtectedSettingsViewStatus.ManagementPrompt, f.Presenter.Last.Status);
        await f.Coordinator.SubmitManagementAuthorizationAsync(request.CorrelationId, "temporary-password".AsMemory());
        Assert.Equal(1, calls);
        Assert.Equal(ProtectedSettingsViewStatus.Completed, f.Presenter.Last.Status);
    }

    [Fact]
    public async Task Support_Prompt_Exposes_Request_Information_And_Code_Prompt()
    {
        Fixture f = new()
        {
            AuthorizationDecision = new(SecureOperationPresentationResult.ManagementAuthorizationRequired),
            ManagementDecision = new(SecureOperationPresentationResult.SupportAuthorizationRequired, "device-42", "request-17")
        };
        EsdAdjustmentChangeRequest request = f.EsdRequest(0m, 4m);
        await f.Coordinator.RequestEsdAdjustmentChangeAsync(request, _ => Task.CompletedTask);
        await f.Coordinator.SubmitManagementAuthorizationAsync(request.CorrelationId, "management".AsMemory());
        Assert.Equal(ProtectedSettingsViewStatus.SupportInformation, f.Presenter.Last.Status);
        Assert.Equal("device-42", f.Presenter.Last.DeviceId);
        Assert.Equal("request-17", f.Presenter.Last.RequestInformation);
        Assert.True(f.Presenter.Last.OneTimeCodePromptVisible);
    }

    [Theory]
    [InlineData(AuthorizationFailureKind.Invalid, "invalid")]
    [InlineData(AuthorizationFailureKind.Expired, "expired")]
    [InlineData(AuthorizationFailureKind.Replayed, "already used")]
    public async Task Invalid_Support_Authorization_Maps_Safe_Feedback(AuthorizationFailureKind kind, string expected)
    {
        Fixture f = new()
        {
            AuthorizationDecision = new(SecureOperationPresentationResult.ManagementAuthorizationRequired),
            ManagementDecision = new(SecureOperationPresentationResult.SupportAuthorizationRequired, "device", "request"),
            SupportDecision = new(SecureOperationPresentationResult.InvalidAuthorization, FailureKind: kind)
        };
        EsdAdjustmentChangeRequest request = f.EsdRequest(0m, 3m);
        int calls = 0;
        await f.Coordinator.RequestEsdAdjustmentChangeAsync(request, _ => { calls++; return Task.CompletedTask; });
        await f.Coordinator.SubmitManagementAuthorizationAsync(request.CorrelationId, "management".AsMemory());
        await f.Coordinator.SubmitSupportAuthorizationAsync(request.CorrelationId, "one-time-secret".AsMemory());
        Assert.Equal(0, calls);
        Assert.Contains(expected, f.Presenter.Last.Feedback, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Expired_Session_Denies_Before_Authorization_And_Execution()
    {
        Fixture f = new();
        f.Session.CurrentValue = f.Session.CurrentValue! with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        int calls = 0;
        await f.Coordinator.RequestCredentialManagementAsync(f.CredentialRequest(), _ => { calls++; return Task.CompletedTask; });
        Assert.Equal(0, calls);
        Assert.Equal(0, f.Authorization.AuthorizeCalls);
        Assert.Equal(ProtectedSettingsViewStatus.SessionExpired, f.Presenter.Last.Status);
    }

    [Fact]
    public async Task Audit_Is_Written_Before_Authorized_Execution()
    {
        var sequence = new List<string>();
        Fixture f = new(sequence) { AuthorizationDecision = new(SecureOperationPresentationResult.Authorized) };
        await f.Coordinator.RequestCredentialManagementAsync(f.CredentialRequest(), _ => { sequence.Add("execute"); return Task.CompletedTask; });
        Assert.Equal(["authorize", "audit", "execute"], sequence);
        Assert.Equal(f.Session.CurrentValue!.ShiftProfileId, f.Audit.Items.Single().InitiatingShiftProfileId);
        Assert.Equal("Rasht", f.Audit.Items.Single().StationId);
    }

    [Fact]
    public async Task Execution_Failure_Is_Generic_And_Does_Not_Leak_Exception()
    {
        Fixture f = new() { AuthorizationDecision = new(SecureOperationPresentationResult.Authorized) };
        await f.Coordinator.RequestSecuritySettingChangeAsync(f.SecurityRequest(), _ => throw new InvalidOperationException("password=do-not-leak"));
        Assert.Equal(SecureOperationPresentationResult.ExecutionFailed, f.Presenter.Last.Result);
        Assert.DoesNotContain("do-not-leak", f.Presenter.Last.Feedback, StringComparison.Ordinal);
    }

    [Fact]
    public void Presentation_And_Ordinary_Request_Contracts_Exclude_Secret_Fields()
    {
        Type[] types = [typeof(ProtectedSettingsViewState), typeof(ProtectedSettingsSnapshot), typeof(EsdAdjustmentChangeRequest), typeof(SecuritySettingChangeRequest), typeof(CredentialManagementRequest), typeof(ProtectedAuthorizationDecision), typeof(ProtectedOperationAuditDecision)];
        string[] forbidden = ["password", "hash", "salt", "secret", "token"];
        foreach (Type type in types)
            foreach (var property in type.GetProperties())
            {
                Assert.DoesNotContain(forbidden, word => property.Name.Contains(word, StringComparison.OrdinalIgnoreCase));
                Assert.False(property.Name.Contains("code", StringComparison.OrdinalIgnoreCase) && property.PropertyType != typeof(bool));
                Assert.NotEqual(typeof(ReadOnlyMemory<char>), property.PropertyType);
            }
    }

    [Fact]
    public async Task Unconfigured_Feature_Falls_Back_To_Legacy_Without_Pilot_Access()
    {
        Fixture f = new() { Mode = SettingsPilotFeatureMode.Legacy };
        await f.Rebuild().Coordinator.ViewAsync();
        Assert.Equal(1, f.Legacy.Calls);
        Assert.Equal(0, f.Reader.Calls);
        Assert.Equal(ProtectedSettingsViewStatus.LegacyFallback, f.Presenter.Last.Status);
    }

    private sealed class Fixture
    {
        private readonly List<string>? _sequence;
        public CapturingPresenter Presenter { get; } = new();
        public SessionProvider Session { get; } = new() { CurrentValue = new("operator", "Rasht", DateTimeOffset.UtcNow.AddHours(1)) };
        public Reader Reader { get; } = new();
        public Legacy Legacy { get; } = new();
        public Authorization Authorization { get; }
        public Audit Audit { get; }
        public SettingsPilotFeatureMode Mode { get; set; } = SettingsPilotFeatureMode.Pilot;
        public ProtectedAuthorizationDecision AuthorizationDecision { set => Authorization.Initial = value; }
        public ProtectedAuthorizationDecision ManagementDecision { set => Authorization.Management = value; }
        public ProtectedAuthorizationDecision SupportDecision { set => Authorization.Support = value; }
        public ProtectedSettingsUiCoordinator Coordinator { get; private set; } = null!;

        public Fixture(List<string>? sequence = null)
        {
            _sequence = sequence;
            Authorization = new(sequence);
            Audit = new(sequence);
            Rebuild();
        }

        public Fixture Rebuild()
        {
            Coordinator = new(Presenter, Session, Reader, Legacy, new FeatureProvider(this), Authorization, Audit);
            return this;
        }

        public EsdAdjustmentChangeRequest EsdRequest(decimal current, decimal proposed) => new(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, current, proposed);
        public SecuritySettingChangeRequest SecurityRequest() => new(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "audit.retention");
        public CredentialManagementRequest CredentialRequest() => new(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "rotate", "operator-2");

        private sealed class FeatureProvider(Fixture owner) : ISettingsPilotFeatureModeProvider
        {
            public SettingsPilotFeatureMode GetMode(string featureKey) => owner.Mode;
        }
    }

    private sealed class CapturingPresenter : IProtectedSettingsPresenter
    {
        public List<ProtectedSettingsViewState> States { get; } = [];
        public ProtectedSettingsViewState Last => States.Last();
        public void Present(ProtectedSettingsViewState state) => States.Add(state);
    }

    private sealed class SessionProvider : IProtectedSettingsSessionProvider
    {
        public ProtectedSettingsSession? CurrentValue { get; set; }
        public ProtectedSettingsSession? Current => CurrentValue;
    }

    private sealed class Reader : IProtectedSettingsReader
    {
        public int Calls { get; private set; }
        public Task<ProtectedSettingsSnapshot> ReadAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new ProtectedSettingsSnapshot("Rasht", 0m, false, new Dictionary<string, string>()));
        }
    }

    private sealed class Legacy : IProtectedSettingsLegacyWorkflow
    {
        public int Calls { get; private set; }
        public Task ShowAsync(CancellationToken cancellationToken) { Calls++; return Task.CompletedTask; }
    }

    private sealed class Authorization(List<string>? sequence) : IProtectedSettingsAuthorizationGateway
    {
        public int AuthorizeCalls { get; private set; }
        public ProtectedAuthorizationDecision Initial { get; set; } = new(SecureOperationPresentationResult.Denied);
        public ProtectedAuthorizationDecision Management { get; set; } = new(SecureOperationPresentationResult.InvalidAuthorization);
        public ProtectedAuthorizationDecision Support { get; set; } = new(SecureOperationPresentationResult.InvalidAuthorization);
        public Task<ProtectedAuthorizationDecision> AuthorizeAsync(ProtectedSettingsSession session, ProtectedSettingsChangeRequest request, CancellationToken cancellationToken) { AuthorizeCalls++; sequence?.Add("authorize"); return Task.FromResult(Initial); }
        public Task<ProtectedAuthorizationDecision> SubmitManagementAuthorizationAsync(string correlationId, ReadOnlyMemory<char> credential, CancellationToken cancellationToken) => Task.FromResult(Management);
        public Task<ProtectedAuthorizationDecision> SubmitSupportAuthorizationAsync(string correlationId, ReadOnlyMemory<char> oneTimeCode, CancellationToken cancellationToken) => Task.FromResult(Support);
    }

    private sealed class Audit(List<string>? sequence) : IProtectedOperationAuditWriter
    {
        public List<ProtectedOperationAuditDecision> Items { get; } = [];
        public Task WriteAsync(ProtectedOperationAuditDecision decision, CancellationToken cancellationToken) { Items.Add(decision); sequence?.Add("audit"); return Task.CompletedTask; }
    }
}
