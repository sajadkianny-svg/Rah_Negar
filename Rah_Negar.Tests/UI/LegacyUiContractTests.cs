using Rah_Negar.Core;
using Rah_Negar.Utils;
using Rah_Negar.UI.Forms.Base;
using System.Drawing;
using System.Windows.Forms;

namespace Rah_Negar.Tests.UI;

public sealed class LegacyUiContractTests
{
    [Fact]
    public void Rasht_grid_preserves_legacy_order_titles_and_calculated_indexes()
    {
        GridProfile profile = GridProfileProvider.GetProfile("Rasht Station");

        AssertProfile(profile,
        [
            "Time", "Inlet Press.", "Outlet Press.", "FirstLine Press.", "40in Press.", "30in Press.",
            "Unit1 Status", "Unit1 RPM", "Unit2 Status", "Unit2 RPM", "Unit3 Status", "Unit3 RPM",
            "Recycle", "Flow", "Inlet Temp", "Outlet Temp", "Ambient Temp", "Ratio"
        ], hourColumnIndex: 0, ratioColumnIndex: 17);
    }

    [Fact]
    public void Ramsar_and_supported_generic_grids_preserve_legacy_order_rules()
    {
        AssertProfile(GridProfileProvider.GetProfile("Ramsar Station"),
        [
            "Time", "Inlet Press.", "Outlet Press.",
            "Unit1 Status", "Unit1 RPM", "Unit2 Status", "Unit2 RPM",
            "Unit3 Status", "Unit3 RPM", "Unit4 Status", "Unit4 RPM",
            "Recycle", "Flow", "Inlet Temp", "Outlet Temp", "Ambient Temp", "Ratio"
        ], hourColumnIndex: 0, ratioColumnIndex: 16);

        foreach (int unitCount in new[] { 3, 4, 5 })
        {
            GridProfile profile = GridProfileProvider.GetProfile(GenericProfileIdentity.Create(unitCount));
            string[] expected =
            [
                "Time", "Inlet Press.", "Outlet Press.",
                .. Enumerable.Range(1, unitCount).SelectMany(unit => new[] { $"Unit{unit} Status", $"Unit{unit} RPM" }),
                "Recycle", "Flow", "Inlet Temp", "Outlet Temp", "Ambient Temp", "Ratio"
            ];

            AssertProfile(profile, expected, hourColumnIndex: 0, ratioColumnIndex: expected.Length - 1);
        }
    }

    [Fact]
    public void Records_event_grid_preserves_legacy_field_titles_and_sequence()
    {
        string designer = File.ReadAllText(Path.Combine(RepositoryRoot(), "UI", "Forms", "FrmRecords.Designer.cs"));

        Assert.Contains("colUnit.HeaderText = \"واحد\"", designer, StringComparison.Ordinal);
        Assert.Contains("colEventType.HeaderText = \"نوع\"", designer, StringComparison.Ordinal);
        Assert.Contains("colEventTime.HeaderText = \"ساعت\"", designer, StringComparison.Ordinal);
        Assert.Contains("colRemark.HeaderText = \"شرح\"", designer, StringComparison.Ordinal);

        int unit = designer.IndexOf("colUnit.HeaderText", StringComparison.Ordinal);
        int type = designer.IndexOf("colEventType.HeaderText", StringComparison.Ordinal);
        int time = designer.IndexOf("colEventTime.HeaderText", StringComparison.Ordinal);
        int remark = designer.IndexOf("colRemark.HeaderText", StringComparison.Ordinal);
        Assert.True(unit < type && type < time && time < remark);
    }

    [Fact]
    public void Surrounding_legacy_forms_restore_approved_persian_captions()
    {
        string records = File.ReadAllText(Path.Combine(RepositoryRoot(), "UI", "Forms", "FrmRecords.Designer.cs"));
        string reports = File.ReadAllText(Path.Combine(RepositoryRoot(), "UI", "Forms", "FrmReportCenter.Designer.cs"));
        string settings = File.ReadAllText(Path.Combine(RepositoryRoot(), "UI", "Forms", "FrmSettings.cs"));

        Assert.Contains("tabPage1.Text = \"پارامترهای عملیاتی\"", records, StringComparison.Ordinal);
        Assert.Contains("tabPage2.Text = \"سوخت، جریان و رویدادها\"", records, StringComparison.Ordinal);
        Assert.Contains("btnSave.Text = \"ثبت\"", records, StringComparison.Ordinal);
        Assert.Contains("label16.Text = \"سوخت و جریان\"", records, StringComparison.Ordinal);
        Assert.Contains("btnGenerateReport.Text = \"تولید گزارش\"", reports, StringComparison.Ordinal);
        Assert.Contains("btnFinalizeMonthlyReport.Text = \"نهایی‌سازی ماه\"", reports, StringComparison.Ordinal);
        Assert.Contains("راه‌اندازی اولیه:", settings, StringComparison.Ordinal);
        Assert.Contains("آخرین پشتیبان‌گیری:", settings, StringComparison.Ordinal);
        Assert.Contains("حجم دیتابیس:", settings, StringComparison.Ordinal);
        Assert.Contains("آخرین تغییر رمز عبور:", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_layout_uses_bounded_fill_for_dynamic_database_details()
    {
        string designer = File.ReadAllText(Path.Combine(RepositoryRoot(), "UI", "Forms", "FrmSettings.Designer.cs"));
        string source = File.ReadAllText(Path.Combine(RepositoryRoot(), "UI", "Forms", "FrmSettings.cs"));

        Assert.Contains("ClientSize = new Size(860, 580)", designer, StringComparison.Ordinal);
        Assert.Contains("MinimumSize = new Size(860, 580)", designer, StringComparison.Ordinal);
        Assert.Contains("lblDatabaseDetails.Dock = DockStyle.Fill", source, StringComparison.Ordinal);
        Assert.Contains("lblDatabaseDetails.AutoSize = false", source, StringComparison.Ordinal);
        Assert.Contains("lblPasswordDetails.AutoSize = false", source, StringComparison.Ordinal);
        Assert.Contains("ApplySettingsLayout();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_visual_conventions_do_not_reorder_or_rename_existing_columns()
    {
        RunSta(() =>
        {
            using TestForm form = new();
            using DataGridView grid = new();
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "col1", HeaderText = "Time" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "col2", HeaderText = "Inlet Press." });
            form.Controls.Add(grid);

            UiStyleService.ApplyFormConventions(form, AppThemeManager.CurrentPalette);

            Assert.Equal(RightToLeft.Yes, form.RightToLeft);
            Assert.True(form.RightToLeftLayout);
            Assert.Equal(new[] { "col1", "col2" }, grid.Columns.Cast<DataGridViewColumn>().Select(x => x.Name));
            Assert.Equal(new[] { "Time", "Inlet Press." }, grid.Columns.Cast<DataGridViewColumn>().Select(x => x.HeaderText));
            Assert.True(grid.RowTemplate.Height >= UiScaleService.Scale(grid, 26));
        });
    }

    private static void AssertProfile(GridProfile profile, IReadOnlyList<string> expected,
        int hourColumnIndex, int ratioColumnIndex)
    {
        Assert.Equal(expected.Count, profile.Columns.Count);
        Assert.Equal(hourColumnIndex, profile.HourColumnIndex);
        Assert.Equal(ratioColumnIndex, profile.RatioColumnIndex);

        for (int index = 0; index < expected.Count; index++)
        {
            Assert.Equal($"col{index + 1}", profile.Columns[index].Name);
            Assert.Equal(expected[index], profile.Columns[index].HeaderText);
        }
    }

    private static string RepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

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

    private sealed class TestForm : BaseForm
    {
        public TestForm() => CreateControl();
    }
}
