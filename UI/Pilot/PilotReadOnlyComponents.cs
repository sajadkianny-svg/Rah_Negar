using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot;
using Rah_Negar.Foundation.Application.Pilot.Presentation;

namespace Rah_Negar.UI.Pilot;

public abstract class PilotReadOnlyValueDisplay : UserControl
{
    private readonly Label _caption;
    private readonly TextBox _value;

    protected PilotReadOnlyValueDisplay(string caption, string accessibleName)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        Dock = DockStyle.Top;
        MinimumSize = new Size(260, 58);
        AccessibleName = accessibleName;
        AccessibleDescription = $"Read-only {caption.ToLowerInvariant()} value.";

        _caption = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Text = caption,
            UseMnemonic = false
        };
        _value = new TextBox
        {
            Dock = DockStyle.Top,
            ReadOnly = true,
            TabStop = true,
            AccessibleName = accessibleName,
            BackColor = SystemColors.Window,
            ForeColor = SystemColors.WindowText
        };
        Controls.Add(_value);
        Controls.Add(_caption);
    }

    public string DisplayedText => _value.Text;

    protected void SetSafeText(string text)
    {
        _value.Text = text;
    }

    protected void SetColors(Color background, Color foreground)
    {
        _value.BackColor = background;
        _value.ForeColor = foreground;
    }
}

public sealed class PilotStatusDisplay : PilotReadOnlyValueDisplay
{
    public PilotStatusDisplay() : base("Execution state", "Pilot execution state") { }

    public void Render(PilotUiViewStatus status)
    {
        (string text, Color background, Color foreground) = status switch
        {
            PilotUiViewStatus.Loading => ("Loading", SystemColors.Control, SystemColors.ControlText),
            PilotUiViewStatus.Completed => ("Completed", Color.Honeydew, Color.DarkGreen),
            PilotUiViewStatus.DifferenceDetected => ("Difference detected", Color.LemonChiffon, Color.DarkGoldenrod),
            PilotUiViewStatus.Blocked => ("Blocked", Color.MistyRose, Color.DarkRed),
            PilotUiViewStatus.Failed => ("Unavailable", SystemColors.Control, SystemColors.ControlText),
            _ => ("Unavailable", SystemColors.Control, SystemColors.ControlText)
        };
        SetSafeText(text);
        SetColors(background, foreground);
    }
}

public sealed class PilotSeverityDisplay : PilotReadOnlyValueDisplay
{
    public PilotSeverityDisplay() : base("Severity", "Pilot comparison severity") { }

    public void Render(ShadowDifferenceSeverity severity)
    {
        (string text, Color background, Color foreground) = severity switch
        {
            ShadowDifferenceSeverity.None => ("None", Color.Honeydew, Color.DarkGreen),
            ShadowDifferenceSeverity.Informational => ("Informational", Color.AliceBlue, Color.DarkBlue),
            ShadowDifferenceSeverity.Warning => ("Warning", Color.LemonChiffon, Color.DarkGoldenrod),
            ShadowDifferenceSeverity.Critical => ("Critical", Color.MistyRose, Color.DarkRed),
            ShadowDifferenceSeverity.Failed => ("Unavailable", SystemColors.Control, SystemColors.ControlText),
            _ => ("Unavailable", SystemColors.Control, SystemColors.ControlText)
        };
        SetSafeText(text);
        SetColors(background, foreground);
    }
}

public sealed class PilotEvidenceSummaryDisplay : PilotReadOnlyValueDisplay
{
    public PilotEvidenceSummaryDisplay() : base("Evidence", "Pilot evidence summary") { }

    public void Render(bool available, string? evidenceReference)
    {
        string? safeReference = PilotSurfaceTextSanitizer.SafeIdentifier(evidenceReference);
        SetSafeText(!available
            ? "Not available"
            : safeReference is null
                ? "Available; reference unavailable"
                : $"Available: {safeReference}");
    }
}

public abstract class PilotMessageListDisplay : UserControl
{
    private readonly TextBox _messages;
    private readonly string _emptyText;
    private readonly string _unsafeFallback;
    private IReadOnlyList<string> _displayedItems = Array.Empty<string>();

    protected PilotMessageListDisplay(
        string caption,
        string accessibleName,
        string emptyText,
        string unsafeFallback)
    {
        _emptyText = emptyText;
        _unsafeFallback = unsafeFallback;
        AutoScaleMode = AutoScaleMode.Dpi;
        Dock = DockStyle.Fill;
        MinimumSize = new Size(260, 100);
        AccessibleName = accessibleName;

        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Text = caption,
            UseMnemonic = false
        };
        _messages = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            TabStop = true,
            AccessibleName = accessibleName,
            BackColor = SystemColors.Window,
            ForeColor = SystemColors.WindowText
        };
        Controls.Add(_messages);
        Controls.Add(label);
        Render(Array.Empty<string>());
    }

    public IReadOnlyList<string> DisplayedItems => _displayedItems;

    public void Render(IEnumerable<string>? messages)
    {
        _displayedItems = PilotSurfaceTextSanitizer.SafeMessages(messages, _unsafeFallback);
        _messages.Text = _displayedItems.Count == 0
            ? _emptyText
            : string.Join(Environment.NewLine, _displayedItems.Select(item => $"- {item}"));
    }
}

public sealed class PilotWarningDisplay : PilotMessageListDisplay
{
    public PilotWarningDisplay() : base("Warnings", "Pilot warnings",
        "No warnings", "Warning details are unavailable.") { }
}

public sealed class PilotBlockedReasonDisplay : PilotMessageListDisplay
{
    public PilotBlockedReasonDisplay() : base("Blocked reasons", "Pilot blocked reasons",
        "No blocked reasons", "A safety condition blocked the pilot result.") { }
}

internal static class PilotSurfaceTextSanitizer
{
    private const int MaximumTextLength = 512;
    private const int MaximumIdentifierLength = 128;

    private static readonly string[] ForbiddenFragments =
    [
        "password", "passwd", "pwd=", "credential", "private key", "secret", "salt",
        "signature", "exception", "stack trace", "sqlite", "sql error", "select ",
        "insert ", "update ", "delete ", "drop ", "alter ", "pragma ", "attach ",
        "create table", "connection string", "authorization"
    ];

    public static string SafeText(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        string candidate = value.Trim();
        if (candidate.Length > MaximumTextLength || candidate.Any(char.IsControl) ||
            candidate.Contains('\\') || candidate.Contains('/') ||
            ForbiddenFragments.Any(fragment => candidate.Contains(fragment,
                StringComparison.OrdinalIgnoreCase)))
            return fallback;
        return candidate;
    }

    public static string? SafeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentifierLength)
            return null;
        return value.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.' or ':') ? value : null;
    }

    public static IReadOnlyList<string> SafeMessages(
        IEnumerable<string>? messages,
        string unsafeFallback)
    {
        if (messages is null) return Array.Empty<string>();
        var safe = new List<string>();
        bool rejected = false;
        foreach (string? message in messages)
        {
            string sanitized = SafeText(message, string.Empty);
            if (sanitized.Length == 0)
                rejected = true;
            else
                safe.Add(sanitized);
        }
        if (rejected) safe.Add(unsafeFallback);
        return Array.AsReadOnly(safe.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
    }
}
