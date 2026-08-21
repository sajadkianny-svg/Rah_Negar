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
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Rah_Negar.UI.Forms.Base;

namespace Rah_Negar.UI.Forms
{
    public partial class FrmSettings : BaseForm
    {


        private int _currentThemeIndex;
        public FrmSettings()
        {
            InitializeComponent();

            LoadSettingsForm();
            //ConfigureThemeRadioButtonsLayout();

            LoadDataBaselineControls();

            cmbDataStartYear.SelectedIndexChanged += (_, _) => UpdateDataBaselineInfo();
            cmbDataStartMonth.SelectedIndexChanged += (_, _) => UpdateDataBaselineInfo();

            KeyPreview = true;
            KeyDown += FrmSettings_KeyDown;

        }

        private void FrmSettings_Load(object sender, EventArgs e)
        {
            LoadDatabaseDetails();
        }
        // ================= Initialization =================

        /// <summary>
        /// جلوگیری از اجرای Event رادیوباتن‌ها هنگام بارگذاری اولیه فرم.
        /// </summary>
        private bool _isLoadingSettings;

        /// <summary>
        /// مقداردهی اولیه فرم تنظیمات بر اساس اطلاعات ذخیره‌شده در دیتابیس.
        /// </summary>
        private void LoadSettingsForm()
        {
            try
            {
                _isLoadingSettings = true;

                AppSettingsModel? settings = AppSettingsService.GetSettings();

                if (settings == null)
                {
                    MessageBox.Show(
                        "تنظیمات برنامه یافت نشد.",
                        "خطا",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    Close();
                    return;
                }

                _currentThemeIndex = settings.ThemeIndex;

                AppThemeManager.LoadThemeByIndex(_currentThemeIndex);
                ApplyThemeToSettingsForm();
                SetThemeRadioButton(_currentThemeIndex);
                LoadNsdRuntimeSettings(settings);
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private void LoadDatabaseDetails()
        {
            string initialSetupText = "-";
            string lastBackupText = "-";
            string passwordChangedText = "-";
            string databaseSizeText = "-";

            using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
            using SqliteCommand cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT
    created_at,
    last_backup_at,
    password_changed_at
FROM app_settings
LIMIT 1;";

            using SqliteDataReader reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                object createdAtValue = reader["created_at"];

                if (createdAtValue != DBNull.Value &&
                    DateTime.TryParse(createdAtValue.ToString(), out DateTime createdAt))
                {
                    initialSetupText = FormatPersianDate(createdAt);
                }

                object lastBackupValue = reader["last_backup_at"];

                if (lastBackupValue != DBNull.Value &&
                    DateTime.TryParse(lastBackupValue.ToString(), out DateTime lastBackup))
                {
                    lastBackupText = FormatPersianDate(lastBackup);
                }

                object passwordChangedValue = reader["password_changed_at"];

                if (passwordChangedValue != DBNull.Value &&
                    DateTime.TryParse(passwordChangedValue.ToString(), out DateTime passwordChanged))
                {
                    passwordChangedText = FormatPersianDate(passwordChanged);
                }
            }

            string databasePath = SqliteDatabaseHelper.GetDatabasePath();

            if (File.Exists(databasePath))
            {
                FileInfo dbFile = new(databasePath);

                databaseSizeText = FormatFileSize(dbFile.Length);
            }

            lblDatabaseDetails.Text =
                $"Initial Setup : {initialSetupText}" +
                Environment.NewLine +
                Environment.NewLine +
                $"Last Backup : {lastBackupText}" +
                Environment.NewLine +
                Environment.NewLine +
                $"Database Size : {databaseSizeText}";

            lblPasswordDetails.Text =
                $"Last Password Change : {passwordChangedText}";
        }

        private static string FormatPersianDate(DateTime dateTime)
        {
            System.Globalization.PersianCalendar pc = new();

            int year = pc.GetYear(dateTime);
            int month = pc.GetMonth(dateTime);
            int day = pc.GetDayOfMonth(dateTime);

            return $"{year:0000}/{month:00}/{day:00}";
        }

        private static string FormatFileSize(long bytes)
        {
            double size = bytes;

            if (size < 1024)
                return $"{size:0} B";

            size /= 1024;

            if (size < 1024)
                return $"{size:0.0} KB";

            size /= 1024;

            if (size < 1024)
                return $"{size:0.0} MB";

            size /= 1024;

            return $"{size:0.0} GB";
        }


        /// <summary>
        /// بارگذاری تنظیمات افزودن ساعت کارکرد بعد از NSD در کنترل‌های فرم.
        /// </summary>
        private void LoadNsdRuntimeSettings(AppSettingsModel settings)
        {
            ChAddHoursAfterEsd.Checked = settings.EsdExtraRuntimeEnabled;
            txtEsdExtraHours.Text = settings.EsdExtraRuntimeHours.ToString("0.##");

            txtEsdExtraHours.Enabled = ChAddHoursAfterEsd.Checked;
        }


        /// <summary>
        /// مقداردهی سال و ماه تاریخ مبنای داده‌ها در فرم تنظیمات
        /// </summary>
        private void LoadDataBaselineControls()
        {
            LoadDataBaselineYears();
            LoadDataBaselineMonths();

            AppSettingsModel? settings = AppSettingsService.GetSettings();

            if (settings != null && settings.DataStartDateRep > 0)
            {
                string value = settings.DataStartDateRep.ToString();

                int year = Convert.ToInt32(value[..4]);
                int month = Convert.ToInt32(value.Substring(4, 2));

                cmbDataStartYear.SelectedItem = year;
                cmbDataStartMonth.SelectedIndex = month - 1;
            }

            bool hasAnyRecord = CommonRecordQueryService.HasAnyDailyRecord();

            //cmbDataStartYear.Enabled = !hasAnyRecord;
            //cmbDataStartMonth.Enabled = !hasAnyRecord;
            //btnUpdateDataStartDate.Enabled = !hasAnyRecord;
            grbBaseLine.Visible = !hasAnyRecord;

            UpdateDataBaselineInfo();

        }

        /// <summary>
        /// مقداردهی سال‌های قابل انتخاب برای تاریخ مبنای داده‌ها
        /// </summary>
        private void LoadDataBaselineYears()
        {
            cmbDataStartYear.Items.Clear();

            int currentPersianYear = GetCurrentPersianYear();

            for (int year = currentPersianYear - 10; year <= currentPersianYear + 10; year++)
                cmbDataStartYear.Items.Add(year);
        }

        /// <summary>
        /// مقداردهی ماه‌های شمسی برای تاریخ مبنای داده‌ها
        /// </summary>
        private void LoadDataBaselineMonths()
        {
            cmbDataStartMonth.Items.Clear();

            cmbDataStartMonth.Items.AddRange(new object[]
            {
        "فروردین",
        "اردیبهشت",
        "خرداد",
        "تیر",
        "مرداد",
        "شهریور",
        "مهر",
        "آبان",
        "آذر",
        "دی",
        "بهمن",
        "اسفند"
            });
        }

        /// <summary>
        /// سال شمسی جاری سیستم را برمی‌گرداند
        /// </summary>
        private static int GetCurrentPersianYear()
        {
            System.Globalization.PersianCalendar calendar = new();

            return calendar.GetYear(DateTime.Now);
        }

        /// <summary>
        /// تاریخ مبنای انتخاب‌شده را به صورت عددی برمی‌گرداند
        /// </summary>
        private long GetSelectedDataStartDateRep()
        {
            if (cmbDataStartYear.SelectedItem == null ||
                cmbDataStartMonth.SelectedIndex < 0)
            {
                return 0;
            }

            int year = Convert.ToInt32(cmbDataStartYear.SelectedItem);
            int month = cmbDataStartMonth.SelectedIndex + 1;

            return year * 10000L + month * 100L + 1;
        }

        /// <summary>
        /// متن راهنمای تاریخ مبنای داده‌ها را به‌روزرسانی می‌کند
        /// </summary>
        private void UpdateDataBaselineInfo()
        {
            long dateRep = GetSelectedDataStartDateRep();

            if (dateRep <= 0)
            {
                txtDataStartDateInfo.Text = "";
                return;
            }

            txtDataStartDateInfo.Text = DateFormatHelper.FormatDateRep(dateRep);
        }

        /// <summary>
        /// تاریخ مبنای شروع داده‌ها را فقط قبل از ثبت اولین داده اصلاح می‌کند
        /// </summary>
        private void btnUpdateDataStartDate_Click(object sender, EventArgs e)
        {
            try
            {
                if (CommonRecordQueryService.HasAnyDailyRecord())
                {
                    MessageBox.Show(
                        "تاریخ مبنای شروع داده‌ها پس از ثبت اولین داده قابل تغییر نیست",
                        "تنظیمات",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                long dataStartDate = GetSelectedDataStartDateRep();

                if (dataStartDate <= 0)
                {
                    MessageBox.Show(
                        "سال و ماه تاریخ مبنای داده‌ها را انتخاب کنید",
                        "اعتبارسنجی",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                DialogResult result = MessageBox.Show(
                    "تاریخ مبنای شروع داده‌ها تغییر خواهد کرد" +
                    Environment.NewLine +
                    Environment.NewLine +
                    "تاریخ جدید" +
                    Environment.NewLine +
                    DateFormatHelper.FormatDateRep(dataStartDate) +
                    Environment.NewLine +
                    Environment.NewLine +
                    "ادامه می‌دهید؟",
                    "تأیید تغییر تاریخ مبنا",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;

                AppSettingsService.SaveDataStartDate(dataStartDate);

                MessageBox.Show(
                    "تاریخ مبنای شروع داده‌ها با موفقیت ذخیره شد",
                    "تنظیمات",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                UpdateDataBaselineInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ذخیره تاریخ مبنای داده‌ها" +
                    Environment.NewLine +
                    ex.Message,
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        // ================= Theme Methods =================

        /// <summary>
        /// رادیوباتن مربوط به تم ذخیره‌شده را انتخاب می‌کند.
        /// </summary>
        private void SetThemeRadioButton(int themeIndex)
        {
            rdoThemeBlue.Checked = themeIndex == 0;
            rdoThemeGraphite.Checked = themeIndex == 1;
            rdoThemeOlive.Checked = themeIndex == 2;
            rdoThemeTerracottaStone.Checked = themeIndex == 3;
            rdoIndigoViolet.Checked = themeIndex == 4;
            rdoIndustrialRed.Checked = themeIndex == 5;
            rdoThemeClassicNeutral.Checked = themeIndex == 6;
            rdoThemeClassicSoftAccent.Checked = themeIndex == 7;

        }

        /// <summary>
        /// شماره تم انتخاب‌شده در فرم را برمی‌گرداند.
        /// </summary>
        private int GetSelectedThemeIndex()
        {
            if (rdoThemeGraphite.Checked)
                return 1;

            if (rdoThemeOlive.Checked)
                return 2;

            if (rdoThemeTerracottaStone.Checked)
                return 3;

            if (rdoIndigoViolet.Checked)
                return 4;

            if (rdoIndustrialRed.Checked)
                return 5;

            if (rdoThemeClassicNeutral.Checked)
                return 6;

            if (rdoThemeClassicSoftAccent.Checked)
                return 7;

            return 0;
        }

        // ================= Event Handlers =================

        /// <summary>
        /// فعال یا غیرفعال کردن مقدار ساعت اضافه‌شونده بعد از NSD.
        /// </summary>
        private void chkAddHoursAfterNsd_CheckedChanged(object? sender, EventArgs e)
        {
            txtEsdExtraHours.Enabled = ChAddHoursAfterEsd.Checked;
        }
        /// <summary>
        /// با تغییر تم انتخاب‌شده، تم فوراً ذخیره و روی فرم اعمال می‌شود.
        /// هنگام بارگذاری اولیه فرم اجرا نمی‌شود.
        /// </summary>
        private void ThemeRadio_CheckedChanged(object? sender, EventArgs e)
        {
            if (_isLoadingSettings)
                return;

            if (sender is not RadioButton radioButton || !radioButton.Checked)
                return;

            _currentThemeIndex = GetSelectedThemeIndex();

            AppThemeManager.LoadThemeByIndex(_currentThemeIndex);
            AppSettingsService.SaveThemeIndex(_currentThemeIndex);

            ApplyThemeToSettingsForm();
        }
        /// <summary>
        /// تم فعال را روی فرم تنظیمات و کنترل‌های اصلی آن اعمال می‌کند.
        /// </summary>
        private void ApplyThemeToSettingsForm()
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            BackColor = palette.FormBackColor;

            pnlHeader.BackColor = palette.HeaderBackColor;
            pnlBody.BackColor = Color.White;
            pnlFooter.BackColor = palette.ContentBackColor;

            lblTitle.ForeColor = palette.TextOnAccentColor;
            lblSubTitle.ForeColor = Color.WhiteSmoke;

            ApplyThemeToGroupBox(gbTheme);
            ApplyThemeToGroupBox(gpDatabase);
            ApplyThemeToGroupBox(gpPassword);

            ApplyThemeToRadioButton(rdoThemeBlue);
            ApplyThemeToRadioButton(rdoThemeGraphite);
            ApplyThemeToRadioButton(rdoThemeOlive);
            ApplyThemeToRadioButton(rdoThemeTerracottaStone);
            ApplyThemeToRadioButton(rdoIndigoViolet);
            ApplyThemeToRadioButton(rdoIndustrialRed);

            ApplyThemeToSettingsButton(btnExportDatabase);
            ApplyThemeToSettingsButton(btnImportDatabase);
            ApplyThemeToSettingsButton(btnRepairDatabase);
            ApplyThemeToSettingsButton(btnChangeLoginPassword);
            ApplyThemeToSettingsButton(btnResetPassword);
            ApplyThemeToSettingsButton(btnAbout);
            ApplyThemeToSettingsButton(btnResetFactory);
            ApplyThemeToSettingsButton(btnClose);
            ApplyThemeToSettingsButton(btnSave);

            UpdateThemeRadioStyles();

            Invalidate();
        }

        // ================= Hidden Runtimes Pannel  =================

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Shift | Keys.H))
            {
                OpenRuntimeSettingsForm();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OpenRuntimeSettingsForm()
        {
            using FrmRuntimeSettings frm = new();
            frm.ShowDialog(this);
        }

        private void FrmSettings_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.H)
            {
                OpenRuntimeSettingsForm();
                e.SuppressKeyPress = true;
            }
        }

        // ================= UI Helper Methods =================


        private void ConfigureThemeRadioButtonsLayout()
        {
            RadioButton[] radios =
            [
                rdoThemeBlue,
                rdoThemeGraphite,
                rdoThemeOlive,
                rdoIndigoViolet,
                rdoIndustrialRed,
                rdoThemeTerracottaStone
            ];

            int radioWidth = 170;
            int radioHeight = 24;
            int startTop = 28;
            int gap = 26;

            int radioLeft = gbTheme.ClientSize.Width - radioWidth - 18;

            for (int i = 0; i < radios.Length; i++)
            {
                RadioButton radio = radios[i];

                radio.AutoSize = false;
                radio.Width = radioWidth;
                radio.Height = radioHeight;
                radio.Left = radioLeft;
                radio.Top = startTop + (i * gap);

                radio.RightToLeft = RightToLeft.No;
                radio.CheckAlign = ContentAlignment.MiddleRight;
                radio.TextAlign = ContentAlignment.MiddleRight;
            }
        }
        /// <summary>
        /// اعمال ظاهر تم روی GroupBox.
        /// </summary>
        private static void ApplyThemeToGroupBox(GroupBox groupBox)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            groupBox.BackColor = Color.White;
            groupBox.ForeColor = Color.FromArgb(35, 35, 35);
            groupBox.Font = new Font("tahoma", 8F, FontStyle.Regular);
        }
        /// <summary>
        /// اعمال ظاهر پایه روی RadioButton.
        /// </summary>
        private static void ApplyThemeToRadioButton(RadioButton radioButton)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            radioButton.BackColor = radioButton.Parent?.BackColor ?? palette.CardBackColor;
            radioButton.ForeColor = Color.FromArgb(45, 45, 45);
            radioButton.FlatStyle = FlatStyle.Flat;
            radioButton.Font = new Font("tahoma", 8F, FontStyle.Regular);
        }
        /// <summary>
        /// ظاهر RadioButtonهای تم را بر اساس انتخاب‌شدن به‌روزرسانی می‌کند.
        /// </summary>
        private void UpdateThemeRadioStyles()
        {
            ApplyThemeRadioCheckedStyle(rdoThemeBlue);
            ApplyThemeRadioCheckedStyle(rdoThemeGraphite);
            ApplyThemeRadioCheckedStyle(rdoThemeOlive);
            ApplyThemeRadioCheckedStyle(rdoThemeTerracottaStone);
            ApplyThemeRadioCheckedStyle(rdoIndigoViolet);
            ApplyThemeRadioCheckedStyle(rdoIndustrialRed);
            ApplyThemeRadioCheckedStyle(rdoThemeClassicSoftAccent);
            ApplyThemeRadioCheckedStyle(rdoThemeClassicNeutral);
        }
        /// <summary>
        /// ظاهر انتخاب‌شده یا عادی یک RadioButton تم را اعمال می‌کند.
        /// </summary>
        private static void ApplyThemeRadioCheckedStyle(RadioButton radioButton)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            if (radioButton.Checked)
            {
                radioButton.ForeColor = palette.PrimaryButtonBackColor;
                radioButton.Font = new Font("tahoma", 8F, FontStyle.Bold);
            }
            else
            {
                radioButton.ForeColor = Color.FromArgb(45, 45, 45);
                radioButton.Font = new Font("tahoma", 8F, FontStyle.Regular);
            }
        }
        /// <summary>
        /// اعمال ظاهر تم روی دکمه‌های فرم تنظیمات.
        /// </summary>
        private static void ApplyThemeToSettingsButton(Button button)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = palette.PrimaryButtonBackColor;
            button.ForeColor = palette.TextOnAccentColor;
            button.FlatAppearance.MouseOverBackColor = palette.PrimaryButtonHoverColor;
            button.FlatAppearance.MouseDownBackColor = palette.PrimaryButtonDownColor;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("tahoma", 8F, FontStyle.Regular);
        }

        // ================= Buttons =================

        /// <summary>
        /// بستن فرم تنظیمات.
        /// </summary>
        private void btnClose_Click(object? sender, EventArgs e)
        {
            Close();
        }
        private void btnChangeLoginPassword_Click(object sender, EventArgs e)
        {
            FrmChangePassword frm = new FrmChangePassword();
            frm.ShowDialog(this);
        }
        private void btnResetPassword_Click(object sender, EventArgs e)
        {
            FrmRecovery frm = new FrmRecovery();
            frm.ShowDialog(this);
        }
        /// <summary>
        /// بازسازی ایندکس‌ها و بهینه‌سازی سبک دیتابیس.
        /// </summary>
        private void btnRepairDatabase_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!ConfirmLoginPassword())
                    return;

                DatabaseMaintenanceService.RepairIndexes();

                MessageBox.Show(
                    "بهینه‌سازی دیتابیس با موفقیت انجام شد",
                    "Repair Database",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در بهینه‌سازی دیتابیس: " + ex.Message,
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        /// <summary>
        /// خروجی گرفتن از فایل دیتابیس در مسیر انتخاب‌شده.
        /// </summary>
        private void btnExportDatabase_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!ConfirmLoginPassword())
                    return;

                AppSettingsModel? settings = AppSettingsService.GetSettings();

                string stationName = settings?.StationName ?? "UnknownStation";
                string safeStationName = MakeSafeFileNamePart(stationName);

                using SaveFileDialog dialog = new()
                {
                    Title = "Export Database",
                    Filter = "Rah Negar Backup (*.rngbak)|*.rngbak",
                    DefaultExt = "rngbak",
                    AddExtension = true,
                    OverwritePrompt = true,
                    FileName = $"RahNegar_{safeStationName}_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.rngbak"
                };

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                DatabaseMaintenanceService.ExportDatabase(dialog.FileName);

                MessageBox.Show(
                    "فایل پشتیبان با موفقیت صادر شد",
                    "Export",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadDatabaseDetails();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string MakeSafeFileNamePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "UnknownStation";

            string result = value.Trim();

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalidChar, '_');
            }

            result = result.Replace(' ', '_');

            while (result.Contains("__"))
                result = result.Replace("__", "_");

            return result.Trim('_');
        }

        private void btnImportDatabase_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!ConfirmLoginPassword())
                    return;

                DialogResult confirm = MessageBox.Show(
                    "با انجام این عملیات، اطلاعات فعلی برنامه با فایل پشتیبان انتخاب‌شده جایگزین می‌شود" +
                    Environment.NewLine +
                    Environment.NewLine +
                    "قبل از ادامه، مطمئن شوید از دیتابیس فعلی نسخه پشتیبان تهیه کرده‌اید" +
                    Environment.NewLine +
                    Environment.NewLine +
                    "ادامه می‌دهید؟",
                    "Import",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes)
                    return;

                using OpenFileDialog dialog = new()
                {
                    Title = "Import Database",
                    Filter = "Rah Negar Backup (*.rngbak)|*.rngbak",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                DatabaseMaintenanceService.ImportDatabase(dialog.FileName);

                MessageBox.Show(
                    "بازیابی اطلاعات با موفقیت انجام شد. برنامه بسته می‌شود",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ================= Security Methods =================

        /// <summary>
        /// فرم تأیید رمز را نمایش می‌دهد و در صورت صحیح بودن، true برمی‌گرداند.
        /// </summary>
        private bool ConfirmLoginPassword()
        {
            using FrmPasswordConfirm frm = new();

            if (frm.ShowDialog(this) != DialogResult.OK)
                return false;

            string password = frm.Password;

            if (string.IsNullOrWhiteSpace(password))
                return false;

            bool isValid = AppSettingsService.VerifyLoginPassword(password);

            if (!isValid)
            {
                MessageBox.Show(
                    "رمز واردشده صحیح نیست",
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            return true;
        }
        private void btnResetFactory_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ConfirmLoginPassword())
                    return;

                DialogResult confirm = MessageBox.Show(
                    "تمام اطلاعات برنامه حذف خواهد شد" +
                    Environment.NewLine +
                    Environment.NewLine +
                    "قبل از ادامه، حتماً از دیتابیس نسخه پشتیبان تهیه کنید" +
                    Environment.NewLine +
                    Environment.NewLine +
                    "ادامه می‌دهید؟",
                    "Factory Reset",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes)
                    return;

                DatabaseMaintenanceService.FactoryReset();

                MessageBox.Show(
                    "ریست انجام شد. برنامه بسته می‌شود",
                    "Done",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            using FrmAbout frm = new();
            frm.ShowDialog(this);
        }
        /// <summary>
        /// ذخیره تنظیمات مربوط به افزودن ساعت کارکرد بعد از NSD.
        /// </summary>
        private void btnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!double.TryParse(txtEsdExtraHours.Text, out double hours))
                {
                    MessageBox.Show(
                        "مقدار ساعت اضافه معتبر نیست",
                        "خطا",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    txtEsdExtraHours.Focus();
                    return;
                }

                if (hours < 0)
                {
                    MessageBox.Show(
                        "مقدار ساعت اضافه نمی‌تواند منفی باشد",
                        "خطا",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    txtEsdExtraHours.Focus();
                    return;
                }

                AppSettingsService.SaveNsdRuntimeSettings(
                    ChAddHoursAfterEsd.Checked,
                    hours);

                MessageBox.Show(
                    "تنظیمات کارکرد واحد ذخیره شد",
                    "تنظیمات",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ذخیره تنظیمات کارکرد واحد:" +
                    Environment.NewLine +
                    ex.Message,
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

    }
}

