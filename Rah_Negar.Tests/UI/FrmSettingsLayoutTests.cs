using Rah_Negar.UI.Forms;
using Rah_Negar.Utils;
using System.Drawing;
using System.Windows.Forms;

namespace Rah_Negar.Tests.UI;

public sealed class FrmSettingsLayoutTests
{
    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    public void Settings_critical_surface_remains_visible_and_bounded(float scale)
    {
        RunSta(() =>
        {
            using FrmSettings form = new(initializeSettings: false);
            form.CreateControl();
            form.Scale(new SizeF(scale, scale));
            form.ClientSize = new Size(Scale(860, scale), Scale(580, scale));
            form.ApplySettingsLayoutForTesting(showDataBaseline: true);
            form.PerformLayout();
            form.Show();
            form.PerformLayout();

            AssertInside(form, Find<Panel>(form, "pnlHeader"));
            AssertInside(form, Find<Panel>(form, "pnlBody"));
            AssertInside(form, Find<Panel>(form, "pnlFooter"));
            AssertInside(Find<Panel>(form, "pnlBody"), Find<GroupBox>(form, "gbTheme"));
            AssertInside(Find<Panel>(form, "pnlBody"), Find<GroupBox>(form, "gpDatabase"));
            AssertInside(Find<Panel>(form, "pnlBody"), Find<GroupBox>(form, "gpPassword"));
            AssertInside(Find<Panel>(form, "pnlBody"), Find<GroupBox>(form, "grpRuntimeSettings"));
            AssertInside(Find<Panel>(form, "pnlBody"), Find<GroupBox>(form, "grbBaseLine"));
            AssertInside(Find<Panel>(form, "panel1"), Find<Label>(form, "lblDatabaseDetails"));
            AssertInside(Find<GroupBox>(form, "gpPassword"), Find<Label>(form, "lblPasswordDetails"));
            Assert.Equal("pnlBody", Find<GroupBox>(form, "grbBaseLine").Parent?.Name);
            AssertInside(Find<Panel>(form, "pnlFooter"), Find<Button>(form, "btnAbout"));
            AssertInside(Find<Panel>(form, "pnlFooter"), Find<Button>(form, "btnResetFactory"));
            AssertInside(Find<Panel>(form, "pnlFooter"), Find<Button>(form, "btnClose"));

            Label databaseDetails = Find<Label>(form, "lblDatabaseDetails");
            Label passwordDetails = Find<Label>(form, "lblPasswordDetails");
            Assert.NotEmpty(databaseDetails.Text);
            Assert.NotEmpty(passwordDetails.Text);

            AssertNoSiblingIntersections(
                Find<GroupBox>(form, "gpDatabase"),
                Find<Panel>(form, "panel1"),
                Find<Button>(form, "btnExportDatabase"),
                Find<Button>(form, "btnImportDatabase"),
                Find<Button>(form, "btnRepairDatabase"));
            AssertNoSiblingIntersections(
                Find<GroupBox>(form, "gpPassword"),
                passwordDetails,
                Find<Button>(form, "btnResetPassword"),
                Find<Button>(form, "btnChangeLoginPassword"));
            AssertNoSiblingIntersections(
                Find<GroupBox>(form, "grpRuntimeSettings"),
                Find<CheckBox>(form, "ChAddHoursAfterEsd"),
                Find<TextBox>(form, "txtEsdExtraHours"),
                Find<Button>(form, "btnSave"));
            AssertNoSiblingIntersections(
                Find<GroupBox>(form, "grbBaseLine"),
                Find<Label>(form, "label1"),
                Find<Label>(form, "label2"),
                Find<ComboBox>(form, "cmbDataStartYear"),
                Find<ComboBox>(form, "cmbDataStartMonth"),
                Find<TextBox>(form, "txtDataStartDateInfo"),
                Find<Button>(form, "btnUpdateDataStartDate"));

            foreach (Button button in FindAll<Button>(form))
            {
                Assert.True(button.Height >= button.Font.Height + button.Padding.Vertical + 2,
                    $"{button.Name} is too short for its themed font and padding");
            }

            foreach (RadioButton radio in FindAll<RadioButton>(Find<GroupBox>(form, "gbTheme")))
                AssertSingleLineFits(radio);

            AssertSingleLineFits(Find<CheckBox>(form, "ChAddHoursAfterEsd"));
            AssertSingleLineFits(Find<Label>(form, "label1"));
            AssertSingleLineFits(Find<Label>(form, "label2"));
            AssertSingleLineFits(Find<Button>(form, "btnUpdateDataStartDate"));
            foreach (Button button in FindAll<Button>(form))
                AssertButtonTextFits(button);

            AssertMultilineTextFits(databaseDetails);
            AssertSingleLineFits(passwordDetails);

            foreach (string buttonName in new[] { "btnAbout", "btnResetFactory", "btnClose" })
            {
                Button button = Find<Button>(form, buttonName);
                Assert.True(button.Bottom + UiScaleService.Scale(form, 14) <= button.Parent!.ClientSize.Height,
                    $"{button.Name} has insufficient bottom margin");
            }
        });
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    public void Settings_without_editable_baseline_keeps_runtime_row_usable(float scale)
    {
        RunSta(() =>
        {
            using FrmSettings form = new(initializeSettings: false);
            form.CreateControl();
            form.Scale(new SizeF(scale, scale));
            form.ClientSize = new Size(Scale(860, scale), Scale(580, scale));
            form.ApplySettingsLayoutForTesting();
            form.PerformLayout();
            form.Show();
            form.PerformLayout();

            GroupBox runtime = Find<GroupBox>(form, "grpRuntimeSettings");
            Assert.False(Find<GroupBox>(form, "grbBaseLine").Visible);
            Assert.True(runtime.Width > Scale(500, scale));
            AssertNoSiblingIntersections(
                runtime,
                Find<CheckBox>(form, "ChAddHoursAfterEsd"),
                Find<TextBox>(form, "txtEsdExtraHours"),
                Find<Button>(form, "btnSave"));
        });
    }

    private static int Scale(int value, float scale) => (int)Math.Round(value * scale);

    private static T Find<T>(Control root, string name) where T : Control =>
        root.Controls.Find(name, true).OfType<T>().Single();

    private static IEnumerable<T> FindAll<T>(Control root) where T : Control =>
        root.Controls.Cast<Control>().SelectMany(child =>
            child is T matching
                ? new[] { matching }.Concat(FindAll<T>(child))
                : FindAll<T>(child));

    private static void AssertSingleLineFits(Control control)
    {
        Size measured = TextRenderer.MeasureText(
            control.Text,
            control.Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

        Assert.True(measured.Width <= control.ClientSize.Width,
            $"{control.Name} text width {measured.Width} exceeds {control.ClientSize.Width}");
        Assert.True(measured.Height <= control.ClientSize.Height,
            $"{control.Name} text height {measured.Height} exceeds {control.ClientSize.Height}");
    }

    private static void AssertMultilineTextFits(Label label)
    {
        foreach (string line in label.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            Size measured = TextRenderer.MeasureText(
                line,
                label.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

            Assert.True(measured.Width <= label.ClientSize.Width,
                $"{label.Name} line width {measured.Width} exceeds {label.ClientSize.Width}");
        }
    }

    private static void AssertButtonTextFits(Button button)
    {
        Size measured = TextRenderer.MeasureText(
            button.Text,
            button.Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

        Assert.True(measured.Width + button.Padding.Horizontal <= button.ClientSize.Width,
            $"{button.Name} caption does not fit its content width");
    }

    private static void AssertNoSiblingIntersections(Control parent, params Control[] controls)
    {
        for (int i = 0; i < controls.Length; i++)
        {
            AssertInside(parent, controls[i]);

            for (int j = i + 1; j < controls.Length; j++)
            {
                Rectangle intersection = Rectangle.Intersect(controls[i].Bounds, controls[j].Bounds);
                Assert.True(intersection.IsEmpty,
                    $"{controls[i].Name} intersects {controls[j].Name} inside {parent.Name}");
            }
        }
    }

    private static void AssertInside(Control parent, Control child)
    {
        Assert.True(child.Visible, $"{child.Name} should be visible");
        Assert.True(child.Left >= 0, $"{child.Name} is left-clipped");
        Assert.True(child.Top >= 0, $"{child.Name} is top-clipped");
        Assert.True(child.Bounds.Right <= parent.ClientSize.Width, $"{child.Name} is right-clipped");
        Assert.True(child.Bounds.Bottom <= parent.ClientSize.Height, $"{child.Name} is bottom-clipped");
    }

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
