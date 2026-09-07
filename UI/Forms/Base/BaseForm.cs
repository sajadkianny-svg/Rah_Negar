using Rah_Negar.Properties;

namespace Rah_Negar.UI.Forms.Base;

public class BaseForm : Form
{
    public const int MaximumSupportedDpi = 144;

    public BaseForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        if (!DesignMode)
            Icon = Resources.AppIcon;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (DeviceDpi > MaximumSupportedDpi)
        {
            MessageBox.Show(this,
                "این مقیاس نمایش پشتیبانی نمی‌شود. لطفاً مقیاس ویندوز را حداکثر روی ۱۵۰٪ تنظیم کنید.",
                "مقیاس نمایش پشتیبانی نمی‌شود", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            Close();
            return;
        }

        ApplyOperatorSurfaceStyle(this);
    }

    private static void ApplyOperatorSurfaceStyle(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Button button)
            {
                button.MinimumSize = new Size(button.MinimumSize.Width, Math.Max(button.MinimumSize.Height, 30));
                button.Padding = new Padding(8, 2, 8, 2);
                button.FlatStyle = FlatStyle.Flat;
            }
            else if (control is TextBox textBox)
            {
                textBox.RightToLeft = RightToLeft.Yes;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.RightToLeft = RightToLeft.Yes;
                comboBox.IntegralHeight = false;
            }

            if (control.HasChildren)
                ApplyOperatorSurfaceStyle(control);
        }
    }
}
