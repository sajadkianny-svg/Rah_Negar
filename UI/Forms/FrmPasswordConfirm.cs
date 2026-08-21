using Rah_Negar.Core;
using Rah_Negar.Services.UI;
using Rah_Negar.UI.Forms.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rah_Negar.UI.Forms
{
    public partial class FrmPasswordConfirm : BaseForm
    {

        public string Password { get; private set; } = string.Empty;

        public FrmPasswordConfirm()
        {
            InitializeComponent();
            ApplyTheme();

            AcceptButton = btnOk;
        }

        private void FrmPasswordConfirm_Load(object sender, EventArgs e)
        {

        }


        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            txtPassword.Focus();
        }

        /// <summary>
        /// تم فعال برنامه را روی فرم اعمال می‌کند.
        /// </summary>
        private void ApplyTheme()
        {
            var palette = AppThemeManager.CurrentPalette;

            BackColor = palette.FormBackColor;

            pnlHeader.BackColor = palette.HeaderBackColor;

            lblTitle.ForeColor = palette.TextOnAccentColor;
            lblPassword.ForeColor = palette.TextPrimaryColor;

            txtPassword.BackColor = palette.CardBackColor;
            txtPassword.ForeColor = palette.TextPrimaryColor;
            txtPassword.BorderStyle = BorderStyle.FixedSingle;

            ApplyButtonStyle(btnOk);
        }

        private static void ApplyButtonStyle(Button button)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = palette.PrimaryButtonBackColor;
            button.ForeColor = palette.TextOnAccentColor;
            button.FlatAppearance.MouseOverBackColor = palette.PrimaryButtonHoverColor;
            button.FlatAppearance.MouseDownBackColor = palette.PrimaryButtonDownColor;
            button.Cursor = Cursors.Hand;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            string pass = txtPassword.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(pass))
            {
                UiMessageService.ShowError("رمز عبور وارد نشده است", "خطا");
                txtPassword.Focus();
                return;
            }

            Password = pass;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
