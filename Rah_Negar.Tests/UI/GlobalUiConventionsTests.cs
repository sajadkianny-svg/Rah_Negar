using Rah_Negar.Core;
using Rah_Negar.Utils;
using System.Drawing;
using System.Windows.Forms;

namespace Rah_Negar.Tests.UI;

public sealed class GlobalUiConventionsTests
{
    [Fact]
    public void Shared_ui_font_rule_is_Tahoma()
    {
        Assert.Equal("Tahoma", UiStyleService.FontFamily);
        using Font font = UiStyleService.CreateFont();
        Assert.Equal("Tahoma", font.Name);
    }

    [Fact]
    public void Shared_conventions_apply_Tahoma_and_true_RTL_to_nested_controls()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using Form form = new();
                using Panel panel = new();
                using FlowLayoutPanel flow = new();
                using DataGridView grid = new();
                grid.Columns.Add("value", "مقدار");
                flow.Controls.Add(new Button { Text = "تأیید" });
                panel.Controls.Add(flow);
                panel.Controls.Add(grid);
                form.Controls.Add(panel);

                UiStyleService.ApplyFormConventions(form, AppThemeManager.CurrentPalette);

                Assert.Equal(RightToLeft.Yes, form.RightToLeft);
                Assert.True(form.RightToLeftLayout);
                Assert.Equal("Tahoma", panel.Font.Name);
                Assert.Equal(RightToLeft.Yes, panel.RightToLeft);
                Assert.Equal(FlowDirection.RightToLeft, flow.FlowDirection);
                Assert.Equal("Tahoma", grid.Font.Name);
                Assert.Equal("Tahoma", grid.DefaultCellStyle.Font.Name);
                Assert.Equal("Tahoma", grid.ColumnHeadersDefaultCellStyle.Font.Name);
                Assert.Equal(RightToLeft.No, grid.RightToLeft);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw new AggregateException(failure);
    }
}
