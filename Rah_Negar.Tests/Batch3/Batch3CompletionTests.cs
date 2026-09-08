using Rah_Negar.Infrastructure.ApplicationData;
using Rah_Negar.Infrastructure.Foundation.Logging;
using Rah_Negar.Services.Reports;
using Rah_Negar.Utils;
using Rah_Negar.Core;
using Rah_Negar.Foundation.Application.Provisioning;
using Rah_Negar.Qualification;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;

namespace Rah_Negar.Tests.Batch3;

public sealed class Batch3CompletionTests
{
    [Fact]
    public void Frequent_combination_summary_is_visible_and_deterministic_for_empty_and_tie_cases()
    {
        Assert.Equal(
            "پرتکرارترین ترکیب واحدها: داده‌ای ثبت نشده است",
            ServiceCombinationSummaryService.Format(new Dictionary<string, int>()));

        string result = ServiceCombinationSummaryService.Format(new Dictionary<string, int>
        {
            ["U2|U3"] = 4,
            ["U1"] = 4,
            ["U4"] = 2
        });

        Assert.Equal("پرتکرارترین ترکیب واحدها: U1 | U2|U3 (4 روز)", result);
    }

    [Fact]
    public void Grid_definition_cache_reuses_metadata_without_rebuilding_columns()
    {
        RunSta(() =>
        {
            DataGridViewDefinitionCache.ClearForTests();
            DataGridViewColumnDefinition[] definitions =
            [new("hour", "ساعت", 60, true, DataGridViewContentAlignment.MiddleCenter, Color.White)];
            using DataGridView grid = new();

            Assert.True(DataGridViewDefinitionCache.EnsureColumns(grid, "batch3-grid", definitions));
            Assert.False(DataGridViewDefinitionCache.EnsureColumns(grid, "batch3-grid", definitions));
            Assert.Single(grid.Columns);
            Assert.Equal("ساعت", grid.Columns[0].HeaderText);

            (long hits, long misses, int count) = DataGridViewDefinitionCache.GetStatistics();
            Assert.True(hits >= 1);
            Assert.Equal(1, misses);
            Assert.Equal(1, count);
        });
    }

    [Fact]
    public void Qualification_root_override_is_isolated_from_ProgramData()
    {
        string isolated = Path.Combine(Path.GetTempPath(), "rah-negar-qualification", Guid.NewGuid().ToString("N"));
        string resolved = ApplicationDataPaths.ResolveDefaultRoot(isolated);

        Assert.Equal(Path.GetFullPath(isolated), resolved);
        Assert.Throws<InvalidOperationException>(() => ApplicationDataPaths.ResolveDefaultRoot(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                ApplicationDataPaths.ProductDirectoryName)));

        string assemblyPath = typeof(ApplicationDataPaths).Assembly.Location;
        string[] productTypeNames = Assembly.LoadFrom(assemblyPath).GetTypes()
            .Select(type => type.FullName ?? string.Empty)
            .ToArray();
        Assert.DoesNotContain(productTypeNames, name => name.Contains("QualificationEnvironment", StringComparison.Ordinal));
    }

    [Fact]
    public void Error_logging_redacts_sensitive_values_and_never_writes_exception_details()
    {
        string logDirectory = Path.Combine(Path.GetTempPath(), "rah-negar-logs", Guid.NewGuid().ToString("N"));
        try
        {
            bool written = ErrorLogger.TryLog(
                new InvalidOperationException("password=secret-value; ordinary context"),
                "batch3-test",
                logDirectory);

            Assert.True(written);
            string log = File.ReadAllText(Directory.GetFiles(logDirectory, "log_*.txt").Single());
            Assert.Contains("password=[REDACTED]", log, StringComparison.Ordinal);
            Assert.Contains("ExceptionType: InvalidOperationException", log, StringComparison.Ordinal);
            Assert.DoesNotContain("secret-value", log, StringComparison.Ordinal);
            Assert.DoesNotContain("System.InvalidOperationException", log, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(logDirectory))
                Directory.Delete(logDirectory, recursive: true);
        }
    }

    [Fact]
    public void Product_source_has_no_dead_recovery_handler_or_data_start_debug_message()
    {
        string root = RepositoryRoot();
        string recoveryDesigner = File.ReadAllText(Path.Combine(root, "UI", "Forms", "FrmRecovery.Designer.cs"));
        string settings = File.ReadAllText(Path.Combine(root, "Services", "AppSettingsService.cs"));
        string report = File.ReadAllText(Path.Combine(root, "UI", "Forms", "FrmReportCenter.cs"));

        Assert.DoesNotContain("NotImplementedException", recoveryDesigner, StringComparison.Ordinal);
        Assert.DoesNotContain("NULL", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", report, StringComparison.Ordinal);
        Assert.Contains("_mostFrequentCombinationLabel.Text", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_acceptance_harness_declares_isolation_inventory_and_never_uses_production_db()
    {
        string root = RepositoryRoot();
        string harness = File.ReadAllText(Path.Combine(root, "Qualification", "run-final-ui-acceptance.ps1"));

        Assert.Contains("RAH_NEGAR_QUALIFICATION_ROOT", harness, StringComparison.Ordinal);
        Assert.Contains("productionDataTouched = $false", harness, StringComparison.Ordinal);
        Assert.Contains("Startup Wizard", harness, StringComparison.Ordinal);
        Assert.Contains("Management authorization dialogs", harness, StringComparison.Ordinal);
        Assert.Contains("dotnet build $toolProject", harness, StringComparison.Ordinal);
        Assert.DoesNotContain("$repo 'Data\\db.sys'", harness, StringComparison.Ordinal);
        Assert.Contains("Qualification\\**\\*.cs", File.ReadAllText(Path.Combine(root, "Rah_Negar.csproj")), StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_acceptance_profiles_cover_supported_counts_and_reject_boundaries()
    {
        foreach (int unitCount in new[] { 3, 4, 5 })
        {
            string name = GenericProfileIdentity.Create(unitCount);
            Assert.True(GenericProfileIdentity.TryGetUnitCount(name, out int parsed));
            Assert.Equal(unitCount, parsed);
            Assert.Equal(unitCount, GenericGridProfileFactory.Create(unitCount).Columns
                .Count(column => column.HeaderText.Contains("Unit", StringComparison.Ordinal)) / 2);
        }

        foreach (int invalidCount in new[] { 2, 6, 35 })
            Assert.False(TargetStationProfileRules.IsUnitCountSupported(invalidCount));

        string harness = File.ReadAllText(Path.Combine(RepositoryRoot(), "Qualification", "run-final-ui-acceptance.ps1"));
        Assert.DoesNotContain("Rasht", harness, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ramsar", harness, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ValidateSet(3, 4, 5)", harness, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_qualification_fixture_is_profile_driven_and_has_the_requested_unit_shape()
    {
        string root = Path.Combine(Path.GetTempPath(), "rah-negar-generic-ui", Guid.NewGuid().ToString("N"));
        try
        {
            QualificationEnvironment.PrepareGeneric(root, 5);
            string scenario = Path.Combine(root, "Generic-5");
            using SqliteConnection connection = new(new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(scenario, "db.sys"),
                Pooling = false
            }.ToString());
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT station_name FROM app_settings;";
            Assert.Equal("Synthetic Qualification Station 5", command.ExecuteScalar()?.ToString());

            command.CommandText = "SELECT name FROM pragma_table_info('tbl_data') WHERE name='u5_rpm';";
            Assert.Equal("u5_rpm", command.ExecuteScalar()?.ToString());

            using JsonDocument profile = JsonDocument.Parse(File.ReadAllText(Path.Combine(scenario, "generic-profile.json")));
            Assert.Equal(5, profile.RootElement.GetProperty("unitCount").GetInt32());
            Assert.False(profile.RootElement.GetProperty("productionDataTouched").GetBoolean());
            Assert.Throws<ArgumentOutOfRangeException>(() => QualificationEnvironment.PrepareGeneric(root, 35));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Installer_sources_enforce_offline_programdata_and_clean_payload_invariants()
    {
        string root = RepositoryRoot();
        string setup = File.ReadAllText(Path.Combine(root, "Installer", "RahNegar.iss"));
        string preparation = File.ReadAllText(Path.Combine(root, "Installer", "prepare-installer.ps1"));
        string validator = File.ReadAllText(Path.Combine(root, "Installer", "validate-installer.ps1"));

        Assert.Contains("PrivilegesRequired=admin", setup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{commonappdata}\\RahNegar", setup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UninstallDelete", setup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("self-contained", preparation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--no-restore", preparation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".pdb", validator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("qualification", validator, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw new AggregateException(failure);
    }
}
