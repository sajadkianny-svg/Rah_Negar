using Microsoft.Data.Sqlite;
using Rah_Negar.Core;
using Rah_Negar.Data;
using Rah_Negar.Models;
using Rah_Negar.Services;
using Rah_Negar.Utils;
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
using Rah_Negar.UI.Forms.Base;
using Rah_Negar.Services.UI;

namespace Rah_Negar.UI.Forms
{
    public partial class FrmLogin : BaseForm

    {

        public FrmLogin()
        {
            InitializeComponent();
            
            // جلوگیری از flicker
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

            InitializeLoginForm();
            
        }

        private void FrmLogin_Load(object sender, EventArgs e)
        {

        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            txtPass.Focus();
        }
        

        /// <summary>
        /// تنظیمات اصلی برنامه که از دیتابیس خوانده می‌شود
        /// </summary>
        private AppSettingsModel? _appSettings;

        /// <summary>
        /// مقداردهی اولیه فرم لاگین:
        /// خواندن تنظیمات، نمایش نام فارسی ایستگاه، و آماده‌سازی فرم
        /// </summary>
        private void InitializeLoginForm()
        {
            try
            {
                _appSettings = AppSettingsService.GetSettings();

                if (_appSettings == null || !_appSettings.IsInitialized)
                {
                    UiMessageService.ShowError("برنامه هنوز راه‌اندازی اولیه نشده است", "خطا");

                    Close();
                    return;
                }

                lblUserValue.Text = GetPersianStationName(_appSettings.StationName);

                CenterControlX(pnlTextBox);
                CenterControlX(lnkChangePass);
                CenterControlX(lnkForgot);

                CenterControlX(lblUserValue);
                CenterControlX(pnlTextBox);
                CenterControlX(lblTitr);

                CenterControlX(lblDownLine);
                CenterControlX(lblSubTitr);


                txtPass.Focus();
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در بارگذاری فرم ورود", ex, "خطا");

                Close();
            }
        }

        /// <summary>
        /// نام انگلیسی ایستگاه را برای نمایش در فرم لاگین به نام فارسی تبدیل می‌کند
        /// </summary>
        private static string GetPersianStationName(string? stationName)
        {
            if (string.IsNullOrWhiteSpace(stationName))
                return "نامشخص";

            return stationName.Trim() switch
            {
                "Rasht Station" => "تاسیسات تقویت فشار گاز رشـت",
                "Ramsar Station" => "تاسیسات تقویت فشار گاز رامسر",
                _ => stationName.Trim()
            };
        }

        /// <summary>
        /// قرار دادن Label در مرکز فرم در محور X
        /// (با توجه به تغییر طول متن)
        /// </summary>
        private void CenterControlX(Control ctrl)
        {
            Control? parent = ctrl.Parent;
            if (parent == null)
                return;

            int x = (parent.ClientSize.Width - ctrl.Width) / 2;
            ctrl.Left = x;
        }

        /// <summary>
        /// بررسی رمز عبور و ورود به فرم اصلی
        /// </summary>
        private void btnLogin_Click(object sender, EventArgs e)
        {
            try
            {
                if (_appSettings == null)
                {
                    UiMessageService.ShowError("پیکره بندی به درستی صورت نگرفته است", "خطا");
                    return;
                }

                string password = txtPass.Text.Trim();

                if (string.IsNullOrWhiteSpace(password))
                {
                    txtPass.Focus();
                    return;
                }

                bool isValid = PasswordHelper.VerifyPassword(
                    password,
                    _appSettings.UserResetPasswordSalt,
                    _appSettings.UserResetPasswordHash);

                if (!isValid)
                {
                    UiMessageService.ShowError("کلمه عبور نادرست است","خطا");

                    txtPass.SelectAll();
                    txtPass.Focus();
                    return;
                }


                AppSession.Login();

                this.Hide();
                FrmMain main = new FrmMain();
                main.ShowDialog();
                this.Close();
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در هنگام ورود", ex, "خطا");
            }
        }

        /// <summary>
        /// بستن فرم لاگین
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// ایجاد درخواست بازیابی رمز عبور و هدایت کاربر به فرم FrmRecovery
        /// </summary>
        private void lnkForgot_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                if (_appSettings == null)
                {
                    UiMessageService.ShowError("تنظیمات برنامه بارگذاری نشده است", "خطا");
                    return;
                }

                string stationName = _appSettings.StationName;

                using (FrmRecovery frm = new FrmRecovery(stationName))
                {
                    DialogResult result = frm.ShowDialog();

                    if (result == DialogResult.OK)
                    {
                        _appSettings = AppSettingsService.GetSettings();

                        txtPass.Clear();
                        txtPass.Focus();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "FrmLogin.lnkForgot_LinkClicked");
                UiMessageService.ShowError("خطا در باز کردن فرم بازیابی", ex, "خطا");
            }
        }
        private void lnkChangePass_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                using (FrmChangePassword frm = new FrmChangePassword(ChangePasswordMode.Normal))
                {
                    DialogResult result = frm.ShowDialog();

                    if (result == DialogResult.OK)
                    {
                        //چون هش و سالت تغییر کرده بارگذاری مجدد تنظیمات 
                        _appSettings = AppSettingsService.GetSettings();

                        txtPass.Clear();
                        txtPass.Focus();
                    }
                }
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در باز کردن فرم تغییر رمز", ex, "خطا");
            }
        }

        private void pnlBack_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = pnlBack.ClientRectangle;

            // یک بیضی بزرگ‌تر از پنل برای ایجاد fade نرم
            Rectangle glowRect = new Rectangle(
                rect.X - rect.Width / 2,
                rect.Y - rect.Height / 3,
                rect.Width * 2,
                rect.Height * 2
            );

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(glowRect);

                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    // رنگ روشن مرکز
                    brush.CenterColor = Color.FromArgb(180, Color.LightBlue);

                    // رنگ تیره لبه‌ها
                    brush.SurroundColors = new[] { Color.MidnightBlue };

                    // محل مرکز نور
                    brush.CenterPoint = new PointF(
                        rect.Width / 2f,
                        rect.Height / 2f
                    );

                    g.FillRectangle(brush, rect);
                }
            }
        }


        
    }
}
