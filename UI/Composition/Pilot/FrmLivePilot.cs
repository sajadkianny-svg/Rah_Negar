using Rah_Negar.Foundation.Application.Pilot.Live;
using Rah_Negar.Foundation.Application.Pilot.Operational;
using Rah_Negar.UI.Composition.Pilot;
using Rah_Negar.UI.Forms.Base;
using Rah_Negar.UI.Pilot;

namespace Rah_Negar.UI.Forms.Pilot;

public sealed class FrmLivePilot : BaseForm
{
    private readonly PilotDashboardControl _dashboard;
    private readonly LivePilotOperatorSession? _session;
    private readonly Button _start = ActionButton("شروع مشاهده فقط‌خواندنی");
    private readonly Button _complete = ActionButton("تکمیل Pilot");
    private readonly Button _stop = ActionButton("توقف Pilot");
    private readonly Button _return = ActionButton("بازگشت به برنامه فعلی");
    private readonly CancellationTokenSource _lifetime = new();
    private bool _closingAfterStop;
    private bool _operationInProgress;

    public FrmLivePilot(LivePilotCompositionResult composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        _dashboard = composition.Dashboard;
        _session = composition.Session;

        Text = "Pilot / فقط خواندنی";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 720);
        Size = new Size(1120, 820);
        AutoScaleMode = AutoScaleMode.Dpi;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        AccessibleName = "Pilot read-only operator window";

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10),
            WrapContents = false
        };
        actions.Controls.AddRange([_start, _complete, _stop, _return]);
        Controls.Add(_dashboard);
        Controls.Add(actions);

        _start.Enabled = composition.IsReady;
        _complete.Enabled = false;
        _stop.Enabled = false;
        _start.Click += StartClicked;
        _complete.Click += CompleteClicked;
        _stop.Click += StopClicked;
        _return.Click += ReturnClicked;
    }

    public bool AutomaticallyStarts => false;
    public bool ReplacesLegacyWindow => false;
    public bool SwitchesAuthority => false;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_closingAfterStop || _session is null || _session.IsTerminal ||
            _session.Lifecycle == ControlledPilotOperationalLifecycle.Created)
        {
            base.OnFormClosing(e);
            return;
        }

        if (_operationInProgress)
        {
            e.Cancel = true;
            return;
        }

        DialogResult answer = MessageBox.Show(this,
            "نشست Pilot هنوز تکمیل نشده است. آیا نشست آزمایشی متوقف و پنجره بسته شود؟",
            "توقف Pilot فقط‌خواندنی", MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        if (answer != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        _ = StopThenCloseAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _lifetime.Cancel(); }
            catch { }
            _lifetime.Dispose();
            _session?.Dispose();
        }
        base.Dispose(disposing);
    }

    private async void StartClicked(object? sender, EventArgs e)
    {
        if (_session is null || _operationInProgress) return;
        SetBusy(true);
        try
        {
            LivePilotDashboardView view = await _session.StartObservationAsync(_lifetime.Token);
            _dashboard.RenderLive(view);
        }
        catch (OperationCanceledException)
        {
            ShowSafeFailure("عملیات Pilot لغو شد.");
        }
        catch
        {
            ShowSafeFailure("مشاهده فقط‌خواندنی Pilot با خطا متوقف شد.");
        }
        finally
        {
            SetBusy(false);
            RefreshActions();
        }
    }

    private async void CompleteClicked(object? sender, EventArgs e)
    {
        if (_session is null || _operationInProgress) return;
        SetBusy(true);
        try
        {
            _dashboard.RenderLive(await _session.CompleteAsync(_lifetime.Token));
        }
        catch
        {
            ShowSafeFailure("ثبت تکمیل Pilot ناموفق بود.");
        }
        finally
        {
            SetBusy(false);
            RefreshActions();
        }
    }

    private async void StopClicked(object? sender, EventArgs e)
    {
        if (_session is null || _operationInProgress) return;
        await StopOnlyAsync();
    }

    private async void ReturnClicked(object? sender, EventArgs e)
    {
        if (_session is not null && !_session.IsTerminal &&
            _session.Lifecycle != ControlledPilotOperationalLifecycle.Created)
        {
            DialogResult answer = MessageBox.Show(this,
                "برای بازگشت، نشست Pilot متوقف می‌شود. ادامه می‌دهید؟",
                "بازگشت به برنامه فعلی", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            if (answer != DialogResult.Yes) return;
            await StopOnlyAsync();
        }
        _closingAfterStop = true;
        Close();
    }

    private async Task StopOnlyAsync()
    {
        if (_session is null) return;
        SetBusy(true);
        try
        {
            _dashboard.RenderLive(await _session.StopAsync(CancellationToken.None));
        }
        catch
        {
            ShowSafeFailure("توقف نشست Pilot ناموفق بود.");
        }
        finally
        {
            SetBusy(false);
            RefreshActions();
        }
    }

    private async Task StopThenCloseAsync()
    {
        await StopOnlyAsync();
        _closingAfterStop = true;
        Close();
    }

    private void SetBusy(bool busy)
    {
        _operationInProgress = busy;
        UseWaitCursor = busy;
        _start.Enabled = !busy && _session?.Lifecycle ==
            ControlledPilotOperationalLifecycle.Created;
        _complete.Enabled = !busy && _session?.Lifecycle ==
            ControlledPilotOperationalLifecycle.ReviewRequired;
        _stop.Enabled = _complete.Enabled;
        _return.Enabled = !busy;
    }

    private void RefreshActions()
    {
        _start.Enabled = _session?.Lifecycle == ControlledPilotOperationalLifecycle.Created;
        _complete.Enabled = _session?.Lifecycle ==
            ControlledPilotOperationalLifecycle.ReviewRequired;
        _stop.Enabled = _complete.Enabled;
        _return.Enabled = true;
    }

    private void ShowSafeFailure(string message) => MessageBox.Show(this, message,
        "Pilot فقط‌خواندنی", MessageBoxButtons.OK, MessageBoxIcon.Error,
        MessageBoxDefaultButton.Button1,
        MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

    private static Button ActionButton(string text) => new()
    {
        AutoSize = true,
        MinimumSize = new Size(150, 34),
        Text = text,
        Font = new Font("Tahoma", 9f),
        UseVisualStyleBackColor = true,
        AccessibleName = text
    };
}
