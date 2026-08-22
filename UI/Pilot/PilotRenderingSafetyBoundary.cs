namespace Rah_Negar.UI.Pilot;

/// <summary>
/// Contains visual failures inside the inactive observation surface.
/// It never retries, exposes exception text, or invokes external behavior.
/// </summary>
internal static class PilotRenderingSafetyBoundary
{
    public static bool TryUpdate(Action? visualUpdate)
    {
        if (visualUpdate is null) return false;
        try
        {
            visualUpdate();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
