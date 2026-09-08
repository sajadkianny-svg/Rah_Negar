using Rah_Negar.Core;
using Rah_Negar.UI.Forms;
using Rah_Negar.Utils;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace Rah_Negar.Tests.UI;

public sealed class FrmRecordsUiCleanupTests
{
    [Fact]
    public void FrmRecords_event_action_captions_are_persian_in_both_modes()
    {
        RunSta(() =>
        {
            using FrmRecords form = CreateForm();
            DataGridView events = Field<DataGridView>(form, "dgvEvents");
            events.Rows.Add(1, "U1", "NSD", "01:00", "test");

            InvokePrivate(form, "dgvEvents_CellClick", form,
                new DataGridViewCellEventArgs(1, 0));

            Assert.Equal("اعمال تغییرات", Field<Button>(form, "btnAdd").Text);

            InvokePrivate(form, "ClearEventEntryControls");

            Assert.Equal("افزودن", Field<Button>(form, "btnAdd").Text);
            Assert.DoesNotContain("Apply", VisibleCaptions(form), StringComparison.Ordinal);
            Assert.DoesNotContain("Add", VisibleCaptions(form), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void FrmRecords_buttons_reserve_room_for_their_rendered_captions()
    {
        RunSta(() =>
        {
            using FrmRecords form = CreateForm();

            foreach (Button button in Descendants(form).OfType<Button>())
            {
                Assert.Equal(ContentAlignment.MiddleCenter, button.TextAlign);
                Assert.True(button.Height >= button.Font.Height + button.Padding.Vertical + 2,
                    $"{button.Name} is too short");
                Assert.True(button.MinimumSize.Height >= UiScaleService.Scale(button, UiStyleService.StandardButtonHeight),
                    $"{button.Name} lost its minimum height");

                Size measured = TextRenderer.MeasureText(
                    button.Text,
                    button.Font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                Assert.True(measured.Width <= button.ClientSize.Width - button.Padding.Horizontal,
                    $"{button.Name} caption is horizontally clipped");
                Assert.True(measured.Height <= button.ClientSize.Height - button.Padding.Vertical,
                    $"{button.Name} caption is vertically clipped");
            }
        });
    }

    [Fact]
    public void Generic_three_unit_grid_fits_without_horizontal_scrolling()
    {
        RunSta(() =>
        {
            using FrmRecords form = CreateForm();
            GridProfile profile = GenericGridProfileFactory.Create(3);
            SetField(form, "_gridProfile", profile);
            SetField(form, "_stationName", GenericProfileIdentity.Create(3));
            InvokePrivate(form, "ApplyGridProfileToDataGridView", profile);

            DataGridView grid = Field<DataGridView>(form, "dgvData");
            int columnWidth = grid.Columns.Cast<DataGridViewColumn>().Sum(column => column.Width);
            int chromeWidth = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;

            Assert.Equal(profile.Columns.Count, grid.Columns.Count);
            Assert.Equal(ScrollBars.Vertical, grid.ScrollBars);
            Assert.True(columnWidth <= grid.ClientSize.Width - chromeWidth - 2,
                "Generic 3-unit grid columns exceed the available viewport");
        });
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Generic_four_and_five_unit_profiles_keep_their_full_grid_contract(int unitCount)
    {
        RunSta(() =>
        {
            using FrmRecords form = CreateForm();
            GridProfile profile = GenericGridProfileFactory.Create(unitCount);
            SetField(form, "_gridProfile", profile);
            SetField(form, "_stationName", GenericProfileIdentity.Create(unitCount));
            InvokePrivate(form, "ApplyGridProfileToDataGridView", profile);

            DataGridView grid = Field<DataGridView>(form, "dgvData");

            Assert.Equal(profile.Columns.Count, grid.Columns.Count);
            Assert.Equal(profile.Columns.Select(column => column.Name),
                grid.Columns.Cast<DataGridViewColumn>().Select(column => column.Name));
            Assert.Equal(profile.Columns.Select(column => column.HeaderText),
                grid.Columns.Cast<DataGridViewColumn>().Select(column => column.HeaderText));
            Assert.Equal(ScrollBars.Both, grid.ScrollBars);
        });
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    public void FrmRecords_sections_and_footer_remain_inside_bounds_at_supported_dpi(float scale)
    {
        RunSta(() =>
        {
            using FrmRecords form = CreateForm();
            form.Scale(new SizeF(scale, scale));
            form.ClientSize = new Size(Scale(880, scale), Scale(511, scale));
            InvokePrivate(form, "ApplyRecordsLayout");
            form.PerformLayout();

            TabControl tabs = Field<TabControl>(form, "tabControl1");
            TabPage eventsTab = Field<TabPage>(form, "tabPage2");
            Panel fuel = Field<Panel>(form, "pnlBodyUnique");
            Panel events = Field<Panel>(form, "pnlBodyEvents");
            Panel footer = Field<Panel>(form, "pnlButtom");
            DataGridView data = Field<DataGridView>(form, "dgvData");
            Button save = Field<Button>(form, "btnSave");

            Assert.True(tabs.Bounds.Right <= form.ClientSize.Width);
            Assert.True(tabs.Bounds.Bottom < footer.Top);
            Assert.True(fuel.Bounds.Right <= eventsTab.ClientSize.Width);
            Assert.True(events.Bounds.Right <= eventsTab.ClientSize.Width);
            Assert.True(fuel.Bounds.Bottom <= eventsTab.ClientSize.Height);
            Assert.True(events.Bounds.Bottom <= eventsTab.ClientSize.Height);
            Assert.True(data.Bounds.Right <= Field<TabPage>(form, "tabPage1").ClientSize.Width);
            Assert.True(save.Bounds.Bottom <= footer.ClientSize.Height);
        });
    }

    private static FrmRecords CreateForm()
    {
        return new FrmRecords(loadUnitsFromDatabase: false);
    }

    private static string VisibleCaptions(Control root) =>
        string.Join('|', Descendants(root)
            .Where(control => control.Visible)
            .Select(control => control.Text));

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;

            foreach (Control descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static T Field<T>(object instance, string name) where T : class =>
        (T)(FindField(instance.GetType(), name)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing field: {name}"));

    private static void SetField(object instance, string name, object value) =>
        (FindField(instance.GetType(), name)
            ?? throw new InvalidOperationException($"Missing field: {name}"))
        .SetValue(instance, value);

    private static void InvokePrivate(object instance, string name, params object?[] args) =>
        (FindMethod(instance.GetType(), name)
            ?? throw new InvalidOperationException($"Missing method: {name}"))
        .Invoke(instance, args);

    private static FieldInfo? FindField(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            FieldInfo? field = current.GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field is not null)
                return field;
        }

        return null;
    }

    private static MethodInfo? FindMethod(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            MethodInfo? method = current.GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method is not null)
                return method;
        }

        return null;
    }

    private static int Scale(int value, float scale) => (int)Math.Round(value * scale);

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
