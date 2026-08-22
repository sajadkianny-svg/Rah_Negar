using System.Globalization;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot;
using Rah_Negar.Foundation.Application.Pilot.Presentation;

namespace Rah_Negar.UI.Pilot;

/// <summary>
/// Explicitly constructed, read-only pilot evidence surface. It owns no workflow or authority service.
/// </summary>
public sealed class PilotDashboardControl : UserControl,
    IPilotWinFormsStateConsumer,
    IPilotDashboardRefreshTarget
{
    private readonly TextBox _pilotId = ReadOnlyField("Current pilot ID");
    private readonly TextBox _feature = ReadOnlyField("Selected pilot feature");
    private readonly TextBox _comparison = ReadOnlyField("Pilot comparison status");
    private readonly TextBox _rollback = ReadOnlyField("Pilot rollback availability");
    private readonly TextBox _correlation = ReadOnlyField("Pilot correlation ID");
    private readonly TextBox _timestamp = ReadOnlyField("Pilot observation timestamp");
    private readonly PilotStatusDisplay _status = new();
    private readonly PilotSeverityDisplay _severity = new();
    private readonly PilotEvidenceSummaryDisplay _evidence = new();
    private readonly PilotWarningDisplay _warnings = new();
    private readonly PilotBlockedReasonDisplay _blockedReasons = new();
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private PilotDashboardState? _currentState;

    public PilotDashboardControl()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        Dock = DockStyle.Fill;
        MinimumSize = new Size(LayoutContract.MinimumSupportedWidth,
            LayoutContract.MinimumSupportedHeight);
        AccessibleName = "Pilot observation dashboard";
        AccessibleDescription = "Read-only pilot comparison evidence. Legacy remains authoritative.";
        TabStop = true;

        var title = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font(FontFamily.GenericSansSerif, 12f, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 8),
            Text = "Pilot observation dashboard",
            UseMnemonic = false,
            AccessibleName = "Pilot observation dashboard title"
        };

        var identity = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 4, 0, 8)
        };
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddLabeledField(identity, "Current pilot ID", _pilotId, 0, 0);
        AddLabeledField(identity, "Selected feature", _feature, 1, 0);
        AddLabeledField(identity, "Comparison status", _comparison, 0, 1);
        AddLabeledField(identity, "Rollback", _rollback, 1, 1);
        AddLabeledField(identity, "Correlation ID", _correlation, 0, 2);
        AddLabeledField(identity, "Timestamp", _timestamp, 1, 2);

        var indicators = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 8)
        };
        indicators.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        indicators.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        indicators.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        indicators.Controls.Add(_status, 0, 0);
        indicators.Controls.Add(_severity, 1, 0);
        indicators.Controls.Add(_evidence, 2, 0);

        var messages = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 4)
        };
        messages.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        messages.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        messages.Controls.Add(_warnings, 0, 0);
        messages.Controls.Add(_blockedReasons, 1, 0);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 4,
            Dock = DockStyle.Fill,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(title, 0, 0);
        root.Controls.Add(identity, 0, 1);
        root.Controls.Add(indicators, 0, 2);
        root.Controls.Add(messages, 0, 3);
        Controls.Add(root);

        ClearState();
    }

    public PilotUiSurfaceKind SurfaceKind => PilotUiSurfaceKind.EmbeddedPilotPanel;
    public bool AutomaticallyOpens => false;
    public bool ExecutesCommands => false;
    public bool RequestsRefresh => false;
    public bool UsesPolling => false;
    public bool UsesTimer => false;
    public bool StartsBackgroundWork => false;
    public bool ResolvesServices => false;
    public bool RecreatesControlsOnRefresh => false;
    public bool HasState => _currentState is not null;
    public PilotAccessibilityRequirements Accessibility => PilotAccessibilityRequirements.Default;
    public PilotLayoutRequirements LayoutContract => PilotLayoutRequirements.Default;
    public PilotSurfaceSnapshot Snapshot { get; private set; } = EmptySnapshot();

    public Task RenderAsync(PilotDashboardState state, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.CompletedTask;
        if (IsDisposed) return Task.CompletedTask;

        if (Environment.CurrentManagedThreadId != _ownerThreadId)
        {
            if (!IsHandleCreated) return Task.CompletedTask;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!IsDisposed && !cancellationToken.IsCancellationRequested)
                            ReplaceState(state);
                    }
                    finally
                    {
                        completion.TrySetResult();
                    }
                }));
            }
            catch
            {
                completion.TrySetResult();
            }
            return completion.Task;
        }

        ReplaceState(state);
        return Task.CompletedTask;
    }

    public void ReplaceState(PilotDashboardState? state)
    {
        if (IsDisposed) return;
        if (state is null)
        {
            ApplyFallbackSafely();
            return;
        }
        _currentState = state;
        if (!PilotRenderingSafetyBoundary.TryUpdate(() => ApplyState(state)))
        {
            ApplyFallbackSafely();
        }
    }

    public void ClearState()
    {
        if (IsDisposed) return;
        _currentState = null;
        Snapshot = EmptySnapshot();
        PilotRenderingSafetyBoundary.TryUpdate(() => ApplySnapshot(
            Snapshot, PilotUiViewStatus.Failed,
            ShadowDifferenceSeverity.Failed, false, null));
    }

    private void ApplyState(PilotDashboardState state)
    {
        PilotFeatureViewState featureState = state.FeatureState;
        bool fallback = false;

        string pilotId = PilotSurfaceTextSanitizer.SafeIdentifier(state.ActivePilotId)
            ?? "No active pilot";
        if (state.ActivePilotId is not null && pilotId == "No active pilot") fallback = true;

        string feature = FeatureText(state.SelectedFeature);
        if (feature == "Unknown feature") fallback = true;
        if (state.SelectedFeature != featureState.Feature) fallback = true;
        if (state.ActivePilotId is not null &&
            !StringComparer.Ordinal.Equals(state.ActivePilotId, featureState.PilotId)) fallback = true;

        PilotUiViewStatus status = Enum.IsDefined(state.ExecutionStatus)
            ? state.ExecutionStatus
            : PilotUiViewStatus.Failed;
        if (!Enum.IsDefined(state.ExecutionStatus) || featureState.Status != state.ExecutionStatus)
            fallback = true;

        ShadowDifferenceSeverity severity = Enum.IsDefined(featureState.Severity)
            ? featureState.Severity
            : ShadowDifferenceSeverity.Failed;
        if (!Enum.IsDefined(featureState.Severity)) fallback = true;

        string comparison = PilotSurfaceTextSanitizer.SafeText(state.ComparisonSummary,
            "Comparison details are unavailable.");
        if (!StringComparer.Ordinal.Equals(comparison, state.ComparisonSummary?.Trim())) fallback = true;
        if (!StringComparer.Ordinal.Equals(state.ComparisonSummary, featureState.ComparisonSummary))
            fallback = true;

        string correlation = PilotSurfaceTextSanitizer.SafeIdentifier(featureState.CorrelationId)
            ?? "Correlation unavailable";
        if (correlation == "Correlation unavailable") fallback = true;

        string timestamp = featureState.TimestampUtc.Offset == TimeSpan.Zero &&
            featureState.TimestampUtc != DateTimeOffset.UnixEpoch
            ? featureState.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
            : "Timestamp unavailable";
        if (timestamp == "Timestamp unavailable") fallback = true;

        IReadOnlyList<string> warnings = PilotSurfaceTextSanitizer.SafeMessages(
            featureState.Warnings, "Warning details are unavailable.");
        IReadOnlyList<string> blocked = PilotSurfaceTextSanitizer.SafeMessages(
            featureState.BlockedReasons, "A safety condition blocked the pilot result.");
        if (!warnings.SequenceEqual(featureState.Warnings, StringComparer.Ordinal) ||
            !blocked.SequenceEqual(featureState.BlockedReasons, StringComparer.Ordinal))
            fallback = true;

        string evidence = EvidenceText(state.EvidenceAvailable, featureState.EvidenceReference);
        if (state.EvidenceAvailable &&
            PilotSurfaceTextSanitizer.SafeIdentifier(featureState.EvidenceReference) is null)
            fallback = true;
        string rollback = state.RollbackAvailable ? "Available" : "Not available";
        Snapshot = new(pilotId, feature, StatusText(status), comparison, SeverityText(severity),
            evidence, rollback, correlation, timestamp, warnings, blocked, fallback);
        ApplySnapshot(Snapshot, status, severity, state.EvidenceAvailable,
            featureState.EvidenceReference);
    }

    private void ApplyFallbackSafely()
    {
        _currentState = null;
        Snapshot = FallbackSnapshot();
        PilotRenderingSafetyBoundary.TryUpdate(() => ApplySnapshot(
            Snapshot, PilotUiViewStatus.Failed,
            ShadowDifferenceSeverity.Failed, false, null));
    }

    private void ApplySnapshot(
        PilotSurfaceSnapshot snapshot,
        PilotUiViewStatus status,
        ShadowDifferenceSeverity severity,
        bool evidenceAvailable,
        string? evidenceReference)
    {
        _pilotId.Text = snapshot.PilotId;
        _feature.Text = snapshot.SelectedFeature;
        _comparison.Text = snapshot.ComparisonStatus;
        _rollback.Text = snapshot.RollbackSummary;
        _correlation.Text = snapshot.CorrelationId;
        _timestamp.Text = snapshot.Timestamp;
        _status.Render(status);
        _severity.Render(severity);
        _evidence.Render(evidenceAvailable, evidenceReference);
        _warnings.Render(snapshot.Warnings);
        _blockedReasons.Render(snapshot.BlockedReasons);
    }

    private static PilotSurfaceSnapshot EmptySnapshot() => new(
        "No active pilot", "No feature selected", "Unavailable",
        "No pilot result is displayed.", "Unavailable", "Not available", "Not available",
        "Correlation unavailable", "Timestamp unavailable", Array.Empty<string>(),
        Array.Empty<string>(), false);

    private static PilotSurfaceSnapshot FallbackSnapshot() => new(
        "Pilot unavailable", "Unknown feature", "Unavailable",
        "Pilot display is unavailable; legacy remains authoritative.", "Unavailable",
        "Not available", "Not available", "Correlation unavailable", "Timestamp unavailable",
        ["The pilot display could not render the supplied state."], Array.Empty<string>(), true);

    private static string FeatureText(PilotFeature? feature) => feature switch
    {
        PilotFeature.AuthenticationPilot => "Authentication pilot",
        PilotFeature.ReportingPilot => "Reporting pilot",
        PilotFeature.RuntimeEventPilot => "Runtime and Event pilot",
        PilotFeature.ProtectedSettingsPilot => "Protected settings pilot",
        PilotFeature.ExportPilot => "Export pilot",
        null => "No feature selected",
        _ => "Unknown feature"
    };

    private static string StatusText(PilotUiViewStatus status) => status switch
    {
        PilotUiViewStatus.Loading => "Loading",
        PilotUiViewStatus.Completed => "Completed",
        PilotUiViewStatus.DifferenceDetected => "Difference detected",
        PilotUiViewStatus.Blocked => "Blocked",
        _ => "Unavailable"
    };

    private static string SeverityText(ShadowDifferenceSeverity severity) => severity switch
    {
        ShadowDifferenceSeverity.None => "None",
        ShadowDifferenceSeverity.Informational => "Informational",
        ShadowDifferenceSeverity.Warning => "Warning",
        ShadowDifferenceSeverity.Critical => "Critical",
        _ => "Unavailable"
    };

    private static string EvidenceText(bool available, string? reference)
    {
        if (!available) return "Not available";
        string? safeReference = PilotSurfaceTextSanitizer.SafeIdentifier(reference);
        return safeReference is null ? "Available; reference unavailable" : $"Available: {safeReference}";
    }

    private static TextBox ReadOnlyField(string accessibleName) => new()
    {
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        ReadOnly = true,
        TabStop = true,
        AccessibleName = accessibleName,
        BackColor = SystemColors.Window,
        ForeColor = SystemColors.WindowText,
        Margin = new Padding(4)
    };

    private static void AddLabeledField(
        TableLayoutPanel layout,
        string caption,
        TextBox field,
        int column,
        int row)
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(4)
        };
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Text = caption,
            UseMnemonic = false
        }, 0, 0);
        panel.Controls.Add(field, 0, 1);
        layout.Controls.Add(panel, column, row);
    }
}
