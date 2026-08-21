using Rah_Negar.Properties;

namespace Rah_Negar.UI.Forms.Base;

public class BaseForm : Form
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (!DesignMode)
            Icon = Resources.AppIcon;
    }
}