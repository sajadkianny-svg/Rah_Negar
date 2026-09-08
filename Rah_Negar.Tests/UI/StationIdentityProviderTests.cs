using Rah_Negar.Core;

namespace Rah_Negar.Tests.UI;

public sealed class StationIdentityProviderTests
{
    [Theory]
    [InlineData("ایستگاه تقویت فشار گاز رشت")]
    [InlineData("Configured Operations Station")]
    public void Login_identity_uses_the_canonical_configured_station_name(string configuredName)
    {
        Assert.Equal(configuredName, StationIdentityProvider.ResolveForLogin(configuredName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Generic Profile (3 Units)")]
    [InlineData("Generic Profile (4 Units)")]
    [InlineData("Generic Profile (5 Units)")]
    public void Login_identity_uses_safe_fallback_when_station_name_is_unavailable(string? configuredName)
    {
        Assert.Equal(StationIdentityProvider.FallbackStationName,
            StationIdentityProvider.ResolveForLogin(configuredName));
    }

    [Fact]
    public void Generic_profile_unit_count_never_replaces_configured_station_identity()
    {
        string configuredName = "ایستگاه عملیاتی شماره ۵";

        Assert.Equal(configuredName, StationIdentityProvider.ResolveForLogin(configuredName));
        Assert.DoesNotContain("Generic Profile", StationIdentityProvider.ResolveForLogin(configuredName),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Main_form_uses_the_same_canonical_station_identity_path_as_login()
    {
        string root = RepositoryRoot();
        string mainSource = File.ReadAllText(Path.Combine(root, "UI", "Forms", "FrmMain.cs"));
        string loginSource = File.ReadAllText(Path.Combine(root, "UI", "Forms", "FrmLogin.cs"));

        Assert.Contains("StationIdentityProvider.ResolveForLogin(settings?.StationName)",
            mainSource, StringComparison.Ordinal);
        Assert.Contains("SetConfiguredStationName(_appSettings.StationName)",
            loginSource, StringComparison.Ordinal);
        Assert.Contains("StationIdentityProvider.ResolveForLogin(configuredStationName)",
            loginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetPersianStationName", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Unknown", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Generic Profile", mainSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_export_presentation_uses_canonical_station_identity_without_changing_profile_identity()
    {
        string root = RepositoryRoot();
        string reportSource = File.ReadAllText(Path.Combine(root, "UI", "Forms", "FrmReportCenter.cs"));
        reportSource = reportSource.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("_profileDefinition = settings.ProfileDefinition",
            reportSource, StringComparison.Ordinal);
        Assert.Contains("FileName = $\"Monthly_Final_Report_{_stationDisplayName}_", reportSource,
            StringComparison.Ordinal);
        Assert.Contains("dialog.FileName,\n                    _profileDefinition", reportSource,
            StringComparison.Ordinal);
        Assert.Contains("_reportProfile.StationName", reportSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_profile_identity_and_three_to_five_unit_architecture_remain_intact()
    {
        foreach (int unitCount in new[] { 3, 4, 5 })
        {
            string profileName = GenericProfileIdentity.Create(unitCount);

            Assert.Equal($"Generic Profile ({unitCount} Units)", profileName);
            Assert.True(GenericProfileIdentity.TryGetUnitCount(profileName, out int parsed));
            Assert.Equal(unitCount, parsed);
            Assert.Equal(unitCount, GenericGridProfileFactory.Create(unitCount).Columns
                .Count(column => column.HeaderText.Contains("Unit", StringComparison.Ordinal)) / 2);
        }
    }

    private static string RepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
