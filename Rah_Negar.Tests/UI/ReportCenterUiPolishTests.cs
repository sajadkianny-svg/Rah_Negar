using Rah_Negar.Core;
using Rah_Negar.UI.Forms;
using Rah_Negar.Utils;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace Rah_Negar.Tests.UI;

public sealed class ReportCenterUiPolishTests
{
    [Fact]
    public void Report_center_runtime_captions_are_persian_and_report_actions_remain_visible()
    {
        RunSta(() =>
        {
            using TestReportCenter form = new();
            form.Show();
            InvokePrivate(form, "ApplyPersianCaptions");

            Assert.Equal("مرکز گزارش", form.Text);
            Assert.Equal("مرکز گزارش و تحلیل", Field<Label>(form, "lblTitle").Text);

            string captions = string.Join('|',
                Field<Label>(form, "lblTitle").Text,
                Field<Label>(form, "label1").Text,
                Field<Label>(form, "label2").Text,
                Field<Button>(form, "btnGenerateReport").Text,
                Field<Button>(form, "btnFinalizeMonthlyReport").Text,
                Field<Button>(form, "btnPDF").Text);

            Assert.DoesNotContain("ReportCenter", captions, StringComparison.Ordinal);
            Assert.DoesNotContain("Analytics Dashboard", captions, StringComparison.Ordinal);
            Assert.DoesNotContain("Run Analysis", captions, StringComparison.Ordinal);
            Assert.DoesNotContain("Finalize", captions, StringComparison.Ordinal);
            Assert.DoesNotContain("PDF Report", captions, StringComparison.Ordinal);
            Assert.DoesNotContain("Year", captions, StringComparison.Ordinal);
            Assert.DoesNotContain("Month", captions, StringComparison.Ordinal);

            Assert.True(Field<Button>(form, "btnGenerateReport").Visible);
            Assert.True(Field<Button>(form, "btnFinalizeMonthlyReport").Visible);
            Assert.True(Field<Button>(form, "btnPDF").Visible);
        });
    }

    [Fact]
    public void Report_center_empty_state_is_centered_and_hides_when_report_pages_are_shown()
    {
        RunSta(() =>
        {
            using TestReportCenter form = new();
            form.Show();
            Label emptyState = Field<Label>(form, "_emptyStateLabel");

            Assert.Equal(DockStyle.Fill, emptyState.Dock);
            Assert.Equal(ContentAlignment.MiddleCenter, emptyState.TextAlign);
            Assert.Equal(RightToLeft.Yes, emptyState.RightToLeft);
            Assert.Contains("تولید گزارش", emptyState.Text, StringComparison.Ordinal);

            InvokePrivate(form, "SetInitialEmptyState");
            Assert.True(emptyState.Visible);

            InvokePrivate(form, "ShowReportPagesAfterGenerate");
            Assert.False(emptyState.Visible);
        });
    }

    [Fact]
    public void Shared_button_style_reserves_vertical_room_and_centers_persian_captions()
    {
        RunSta(() =>
        {
            using TestForm form = new();
            using Button button = new() { Text = "افزودن", Size = new Size(110, 26) };
            form.Controls.Add(button);
            form.CreateControl();
            button.CreateControl();

            UiStyleService.ApplyButtonConventions(button, AppThemeManager.CurrentPalette);

            int minimumHeight = UiScaleService.Scale(button, UiStyleService.StandardButtonHeight);
            Assert.True(button.MinimumSize.Height >= minimumHeight);
            Assert.True(button.Height >= minimumHeight);
            Assert.Equal(ContentAlignment.MiddleCenter, button.TextAlign);
            Assert.True(button.Padding.Top >= UiScaleService.Scale(button, 4));
            Assert.True(button.Padding.Bottom >= UiScaleService.Scale(button, 4));
        });
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    public void Report_center_actions_and_empty_state_fit_supported_dpi_scales(float scale)
    {
        RunSta(() =>
        {
            using TestReportCenter form = new();
            form.CreateControl();
            form.Scale(new SizeF(scale, scale));
            form.ClientSize = new Size(Scale(784, scale), Scale(461, scale));
            InvokePrivate(form, "ApplyPersianCaptions");
            InvokePrivate(form, "ApplyThemeToReportForm");
            form.PerformLayout();
            form.Show();
            form.PerformLayout();

            foreach (string buttonName in new[] { "btnGenerateReport", "btnFinalizeMonthlyReport", "btnPDF" })
            {
                Button button = Field<Button>(form, buttonName);
                Assert.True(button.Height >= button.Font.Height + button.Padding.Vertical + 2,
                    $"{buttonName} is too short at scale {scale}");
                AssertButtonTextFits(button);
            }

            Label emptyState = Field<Label>(form, "_emptyStateLabel");
            Assert.True(emptyState.ClientSize.Width > emptyState.Padding.Horizontal);
            Assert.True(emptyState.ClientSize.Height > emptyState.Padding.Vertical);
            AssertMultilineTextFits(emptyState);
        });
    }

    [Fact]
    public void FrmRecords_legacy_event_column_titles_and_order_remain_unchanged()
    {
        string designer = File.ReadAllText(Path.Combine(RepositoryRoot(), "UI", "Forms", "FrmRecords.Designer.cs"));

        string[] columns = ["colUnit", "colEventType", "colEventTime", "colRemark"];
        string[] titles = ["واحد", "نوع", "ساعت", "شرح"];

        int previous = -1;
        for (int index = 0; index < columns.Length; index++)
        {
            string marker = $"{columns[index]}.HeaderText = \"{titles[index]}\"";
            int position = designer.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(position > previous, $"Missing or reordered legacy marker: {marker}");
            previous = position;
        }

        Assert.Contains("Controls.Add(tabPage1);", designer, StringComparison.Ordinal);
        Assert.Contains("Controls.Add(tabPage2);", designer, StringComparison.Ordinal);
    }

    private static T Field<T>(object instance, string name) where T : class =>
        (T)(FindField(instance.GetType(), name)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing field: {name}"));

    private static void InvokePrivate(object instance, string name) =>
        (FindMethod(instance.GetType(), name)
            ?? throw new InvalidOperationException($"Missing method: {name}")).Invoke(instance, null);

    private static FieldInfo? FindField(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            FieldInfo? field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field is not null)
                return field;
        }

        return null;
    }

    private static MethodInfo? FindMethod(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            MethodInfo? method = current.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method is not null)
                return method;
        }

        return null;
    }

    private static string RepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static int Scale(int value, float scale) => (int)Math.Round(value * scale);

    private static void AssertButtonTextFits(Button button)
    {
        Size measured = TextRenderer.MeasureText(
            button.Text,
            button.Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

        Assert.True(measured.Width <= button.ClientSize.Width - button.Padding.Horizontal,
            $"{button.Name} text width {measured.Width} exceeds its client width");
        Assert.True(measured.Height <= button.ClientSize.Height - button.Padding.Vertical,
            $"{button.Name} text height {measured.Height} exceeds its client height");
    }

    private static void AssertMultilineTextFits(Label label)
    {
        Size measured = TextRenderer.MeasureText(
            label.Text,
            label.Font,
            new Size(label.ClientSize.Width - label.Padding.Horizontal, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

        Assert.True(measured.Height <= label.ClientSize.Height - label.Padding.Vertical,
            $"{label.Name} text height {measured.Height} exceeds its client height");
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

    private sealed class TestForm : Form
    {
        public TestForm() => CreateControl();
    }

    private sealed class TestReportCenter : FrmReportCenter
    {
        protected override void OnLoad(EventArgs e)
        {
        }
    }
}
