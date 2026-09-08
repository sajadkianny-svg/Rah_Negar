using Rah_Negar.Core;
using Rah_Negar.UI.Forms;
using Rah_Negar.Utils;
using System.Drawing;
using System.Windows.Forms;

namespace Rah_Negar.Tests.UI;

public sealed class FrmLoginLayoutTests
{
    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    public void Login_controls_remain_visible_and_bounded_across_supported_scales(float scale)
    {
        RunSta(() =>
        {
            using FrmLogin form = new(initializeSettings: false);
            form.CreateControl();

            form.Scale(new SizeF(scale, scale));
            form.ClientSize = new Size(Scale(800, scale), Scale(470, scale));
            UiStyleService.ApplyFormConventions(form, AppThemeManager.CurrentPalette);
            form.PerformLayout();
            form.Show();
            form.PerformLayout();

            Panel root = Find<Panel>(form, "pnlBack");
            Panel brand = Find<Panel>(form, "pnlBrand");
            Panel loginArea = Find<Panel>(form, "pnlLoginArea");
            Panel card = Find<Panel>(form, "pnlLoginCard");
            Panel inputRow = Find<Panel>(form, "pnlTextBox");
            TextBox password = Find<TextBox>(form, "txtPass");
            Button login = Find<Button>(form, "btnLogin");

            Assert.Equal(DockStyle.Fill, root.Dock);
            Assert.True(form.ClientSize.Width > form.ClientSize.Height);
            Assert.True(form.MinimumSize.Width > form.MinimumSize.Height);
            Assert.Equal(DockStyle.None, card.Dock);
            Assert.True(card.Visible);
            Assert.Equal(DockStyle.Left, brand.Dock);
            Assert.Equal(DockStyle.Fill, loginArea.Dock);
            Assert.True(card.Width < loginArea.ClientSize.Width);
            Assert.True(card.Height < loginArea.ClientSize.Height);
            AssertControlInside(root, card);
            AssertControlInside(root, brand);
            AssertControlInside(root, loginArea);
            AssertControlInside(card, Find<Label>(form, "lblTitr"));
            AssertControlInside(card, Find<Label>(form, "lblUserValue"));
            AssertControlInside(card, inputRow);
            AssertControlInside(card, Find<LinkLabel>(form, "lnkChangePass"));
            AssertControlInside(card, Find<LinkLabel>(form, "lnkForgot"));
            AssertControlInside(inputRow, password);
            AssertControlInside(inputRow, login);

            Assert.InRange(password.Width, Scale(250, scale), Scale(340, scale));
            Assert.Equal(login, form.AcceptButton);
            Assert.True(password.Width < loginArea.ClientSize.Width);
            Assert.True(login.Bounds.Right <= inputRow.ClientSize.Width);

            form.ClientSize = new Size(Scale(740, scale), Scale(450, scale));
            form.PerformLayout();
            AssertControlInside(Find<Panel>(form, "pnlLoginArea"), card);
        });
    }

    private static int Scale(int value, float scale) => (int)Math.Round(value * scale);

    private static T Find<T>(Control root, string name) where T : Control =>
        root.Controls.Find(name, true).OfType<T>().Single();

    private static void AssertControlInside(Control parent, Control child)
    {
        Assert.True(child.Visible);
        Assert.True(child.Left >= 0);
        Assert.True(child.Top >= 0);
        Assert.True(child.Bounds.Right <= parent.ClientSize.Width);
        Assert.True(child.Bounds.Bottom <= parent.ClientSize.Height);
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
