using Rah_Negar.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rah_Negar.UI.Forms
{
    public partial class FrmAbout : Form
    {
        public FrmAbout()
        {

            InitializeComponent();

            ApplyTheme();

            typeof(Panel)
                .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(pnlHeader, true, null);

        }

        private void pnlHeader_Paint(object sender, PaintEventArgs e)
        {
            DrawHeaderGradient(e.Graphics);
        }

        private void DrawHeaderGradient(Graphics g)
        {
            var palette = AppThemeManager.CurrentPalette;

            Color c1 = palette.HeaderBackColor;
            Color c2 = ControlPaint.Light(palette.HeaderBackColor, 0.65f);

            using LinearGradientBrush brush = new(
                pnlHeader.ClientRectangle,
                c1,
                c2,
                LinearGradientMode.Vertical);

            g.FillRectangle(brush, pnlHeader.ClientRectangle);
        }

        /// <summary>
        /// اعمال تم فعال برنامه روی فرم About.
        /// </summary>
        private void ApplyTheme()
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            // پنل هدر خودش در Paint گرادیان می‌گیرد
            pnlHeader.Invalidate();

        }


        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
