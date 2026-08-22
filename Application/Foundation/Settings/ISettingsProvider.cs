namespace Rah_Negar.Foundation.Application.Settings;

public interface ISettingsProvider
{
    ValueTask<T?> GetApplicationSettingAsync<T>(
        string key,
        CancellationToken cancellationToken = default);

    ValueTask<T?> GetDatabaseSettingAsync<T>(
        string key,
        CancellationToken cancellationToken = default);

    ValueTask<bool> IsFeatureEnabledAsync(
        string featureCode,
        CancellationToken cancellationToken = default);
}
