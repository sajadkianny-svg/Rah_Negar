using Microsoft.Data.Sqlite;
using Rah_Negar.Core;
using Rah_Negar.Data;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Infrastructure.ApplicationData;
using Rah_Negar.Models;
using Rah_Negar.Qualification;
using Rah_Negar.Services;
using Rah_Negar.Services.UI;
using Rah_Negar.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Rah_Negar.UI.Forms.Base;

namespace Rah_Negar.UI.Forms
{
    public partial class FrmSettings : BaseForm
    {


        private int _currentThemeIndex;
        private readonly bool _skipDatabaseLoad;
        private bool _isApplyingSettingsLayout;
        public FrmSettings() : this(initializeSettings: true)
        {
        }

        internal FrmSettings(bool initializeSettings)
        {
            _skipDatabaseLoad = !initializeSettings;
            InitializeComponent();
            CancelButton = btnClose;

            // Factory reset is intentionally unavailable until the managed
            // recovery workflow is composed; make that state explicit in the UI.
            btnResetFactory.Enabled = false;
            btnResetFactory.Text = "بازنشانی کارخانه‌ای (غیرفعال)";
            btnResetFactory.AccessibleName = "بازنشانی کارخانه‌ای؛ در دسترس نیست";
            btnResetFactory.Width = 200;
            btnResetFactory.Left = 403;

            if (initializeSettings)
            {
                LoadSettingsForm();
                //ConfigureThemeRadioButtonsLayout();

                LoadDataBaselineControls();
            }

            cmbDataStartYear.SelectedIndexChanged += (_, _) => UpdateDataBaselineInfo();
            cmbDataStartMonth.SelectedIndexChanged += (_, _) => UpdateDataBaselineInfo();

            KeyPreview = true;
            KeyDown += FrmSettings_KeyDown;

            ApplySettingsLayout();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplySettingsLayout();
        }

        private void FrmSettings_Load(object sender, EventArgs e)
        {
            if (_skipDatabaseLoad)
                return;

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
                        UiMessageService.ShowMessageBox(
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
                $"راه‌اندازی اولیه: {initialSetupText}" +
                Environment.NewLine +
                $"آخرین پشتیبان‌گیری: {lastBackupText}" +
                Environment.NewLine +
                $"حجم دیتابیس: {databaseSizeText}";

            lblPasswordDetails.Text =
                $"آخرین تغییر رمز عبور: {passwordChangedText}";
        }

        private void ApplySettingsLayout()
        {
            if (_isApplyingSettingsLayout)
                return;

            _isApplyingSettingsLayout = true;

            try
            {
                int margin = Scale(14);
                int gap = Scale(10);
                int sectionGap = Scale(10);
                int themeHeight = Scale(92);
                int columnHeight = Scale(148);
                int bottomHeight = Scale(162);
                int buttonHeight = Scale(34);

                pnlHeader.Height = Scale(56);
                pnlFooter.Height = Math.Max(Scale(54), buttonHeight + Scale(28));

                int bodyWidth = Math.Max(0, pnlBody.ClientSize.Width);
                int contentWidth = Math.Max(0, bodyWidth - (margin * 2));

                gbTheme.SetBounds(margin, margin, contentWidth, themeHeight);
                LayoutThemeOptions(margin, gap);

                int columnsTop = gbTheme.Bottom + sectionGap;
                int columnWidth = Math.Max(0, (contentWidth - gap) / 2);
                gpDatabase.SetBounds(margin, columnsTop, columnWidth, columnHeight);
                gpPassword.SetBounds(margin + columnWidth + gap, columnsTop, columnWidth, columnHeight);

                LayoutDatabaseSection(margin, gap, buttonHeight);
                LayoutPasswordSection(margin, buttonHeight);

                int bottomTop = columnsTop + columnHeight + sectionGap;
                int bottomWidth = Math.Max(0, (contentWidth - gap) / 2);

                if (grbBaseLine.Visible)
                {
                    grpRuntimeSettings.SetBounds(margin, bottomTop, bottomWidth, bottomHeight);
                    grbBaseLine.SetBounds(margin + bottomWidth + gap, bottomTop, bottomWidth, bottomHeight);
                }
                else
                {
                    grpRuntimeSettings.SetBounds(margin, bottomTop, contentWidth, bottomHeight);
                    grbBaseLine.SetBounds(0, 0, 0, 0);
                }

                LayoutRuntimeSection(margin, gap, buttonHeight);
                LayoutDataBaselineSection(margin, gap, buttonHeight);
                LayoutFooter(margin, gap, buttonHeight);
            }
            finally
            {
                _isApplyingSettingsLayout = false;
            }
        }

        internal void ApplySettingsLayoutForTesting(bool showDataBaseline = false)
        {
            grbBaseLine.Visible = showDataBaseline;
            ApplySettingsLayout();
        }

        private int Scale(int value) => UiScaleService.Scale(this, value);

        private void LayoutThemeOptions(int margin, int gap)
        {
            RadioButton[] radios =
            [
                rdoThemeClassicNeutral,
                rdoThemeClassicSoftAccent,
                rdoIndustrialRed,
                rdoIndigoViolet,
                rdoThemeTerracottaStone,
                rdoThemeOlive,
                rdoThemeGraphite,
                rdoThemeBlue
            ];

            int rowHeight = Scale(26);
            int rowGap = Scale(4);
            int rowWidth = Math.Max(0, (gbTheme.ClientSize.Width - (margin * 2) - (gap * 3)) / 4);

            for (int i = 0; i < radios.Length; i++)
            {
                RadioButton radio = radios[i];
                int row = i / 4;
                int column = i % 4;
                int right = gbTheme.ClientSize.Width - margin - (column * (rowWidth + gap));

                radio.AutoSize = false;
                radio.RightToLeft = RightToLeft.Yes;
                radio.CheckAlign = ContentAlignment.MiddleRight;
                radio.TextAlign = ContentAlignment.MiddleRight;
                radio.SetBounds(right - rowWidth, Scale(25) + row * (rowHeight + rowGap), rowWidth, rowHeight);
            }
        }

        private void LayoutDatabaseSection(int margin, int gap, int buttonHeight)
        {
            int buttonWidth = Scale(145);
            int panelWidth = Math.Min(Scale(225), Math.Max(0, gpDatabase.ClientSize.Width - (margin * 2) - gap - buttonWidth));
            int top = Scale(29);
            int rowGap = Scale(4);

            panel1.SetBounds(gpDatabase.ClientSize.Width - margin - panelWidth, top, panelWidth, Scale(108));
            panel1.Padding = new Padding(Scale(10), Scale(5), Scale(10), Scale(5));
            panel1.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            lblDatabaseDetails.AutoSize = false;
            lblDatabaseDetails.Dock = DockStyle.Fill;
            lblDatabaseDetails.Padding = Padding.Empty;
            lblDatabaseDetails.RightToLeft = RightToLeft.Yes;
            lblDatabaseDetails.TextAlign = ContentAlignment.MiddleRight;
            lblDatabaseDetails.Font = UiStyleService.CreateFont(8.5f, FontStyle.Bold);

            Button[] buttons = [btnExportDatabase, btnImportDatabase, btnRepairDatabase];
            foreach (Button button in buttons)
            {
                button.AutoSize = false;
                button.MinimumSize = new Size(0, buttonHeight);
            }

            for (int i = 0; i < buttons.Length; i++)
                buttons[i].SetBounds(margin, top + i * (buttonHeight + rowGap), buttonWidth, buttonHeight);
        }

        private void LayoutPasswordSection(int margin, int buttonHeight)
        {
            int buttonWidth = Scale(145);
            int top = Scale(27);
            lblPasswordDetails.AutoSize = false;
            lblPasswordDetails.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblPasswordDetails.SetBounds(margin, top, Math.Max(0, gpPassword.ClientSize.Width - (margin * 2)), Scale(28));
            lblPasswordDetails.RightToLeft = RightToLeft.Yes;
            lblPasswordDetails.TextAlign = ContentAlignment.MiddleRight;
            lblPasswordDetails.Font = UiStyleService.CreateFont(8.5f, FontStyle.Bold);

            Button[] buttons = [btnResetPassword, btnChangeLoginPassword];
            foreach (Button button in buttons)
            {
                button.AutoSize = false;
                button.MinimumSize = new Size(0, buttonHeight);
            }

            int buttonLeft = gpPassword.ClientSize.Width - margin - buttonWidth;
            btnResetPassword.SetBounds(buttonLeft, Scale(61), buttonWidth, buttonHeight);
            btnChangeLoginPassword.SetBounds(buttonLeft, Scale(101), buttonWidth, buttonHeight);
        }

        private void LayoutRuntimeSection(int margin, int gap, int buttonHeight)
        {
            int contentWidth = grpRuntimeSettings.ClientSize.Width;
            int rowTop = Scale(63);
            int inputWidth = Scale(58);
            int saveWidth = Scale(75);
            int checkWidth = Math.Min(Scale(230), Math.Max(Scale(180), contentWidth - (margin * 2) - saveWidth - gap - inputWidth - gap));

            ChAddHoursAfterEsd.AutoSize = false;
            ChAddHoursAfterEsd.RightToLeft = RightToLeft.Yes;
            ChAddHoursAfterEsd.CheckAlign = ContentAlignment.MiddleRight;
            ChAddHoursAfterEsd.TextAlign = ContentAlignment.MiddleRight;
            ChAddHoursAfterEsd.SetBounds(contentWidth - margin - checkWidth, rowTop, checkWidth, buttonHeight);

            int inputLeft = ChAddHoursAfterEsd.Left - gap - inputWidth;
            txtEsdExtraHours.SetBounds(inputLeft, rowTop, inputWidth, buttonHeight);

            int saveLeft = inputLeft - gap - saveWidth;
            btnSave.AutoSize = false;
            btnSave.MinimumSize = new Size(0, buttonHeight);
            btnSave.SetBounds(Math.Max(margin, saveLeft), rowTop, saveWidth, buttonHeight);
        }

        private void LayoutDataBaselineSection(int margin, int gap, int buttonHeight)
        {
            if (!grbBaseLine.Visible)
                return;

            int labelWidth = Scale(52);
            int fieldWidth = Scale(145);
            int actionWidth = Scale(120);
            int labelLeft = grbBaseLine.ClientSize.Width - margin - labelWidth;
            int fieldLeft = labelLeft - gap - fieldWidth;

            label1.AutoSize = false;
            label1.TextAlign = ContentAlignment.MiddleRight;
            label1.SetBounds(labelLeft, Scale(36), labelWidth, buttonHeight);
            label2.AutoSize = false;
            label2.TextAlign = ContentAlignment.MiddleRight;
            label2.SetBounds(labelLeft, Scale(73), labelWidth, buttonHeight);

            cmbDataStartYear.SetBounds(fieldLeft, Scale(36), fieldWidth, buttonHeight);
            cmbDataStartMonth.SetBounds(fieldLeft, Scale(73), fieldWidth, buttonHeight);
            txtDataStartDateInfo.SetBounds(fieldLeft, Scale(110), fieldWidth, buttonHeight);
            btnUpdateDataStartDate.SetBounds(margin, Scale(110), actionWidth, buttonHeight);
        }

        private void LayoutFooter(int margin, int gap, int buttonHeight)
        {
            int top = Math.Max(0, (pnlFooter.ClientSize.Height - buttonHeight) / 2);
            int resetWidth = Scale(220);
            int closeWidth = Scale(88);

            Button[] buttons = [btnAbout, btnClose, btnResetFactory];
            foreach (Button button in buttons)
            {
                button.AutoSize = false;
                button.MinimumSize = new Size(0, buttonHeight);
            }

            btnAbout.SetBounds(margin, top, Scale(100), buttonHeight);
            btnClose.SetBounds(pnlFooter.ClientSize.Width - margin - closeWidth, top, closeWidth, buttonHeight);
            btnResetFactory.SetBounds(btnClose.Left - gap - resetWidth, top, resetWidth, buttonHeight);
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
                    UiMessageService.ShowMessageBox(
                        "تاریخ مبنای شروع داده‌ها پس از ثبت اولین داده قابل تغییر نیست",
                        "تنظیمات",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                long dataStartDate = GetSelectedDataStartDateRep();

                if (dataStartDate <= 0)
                {
                    UiMessageService.ShowMessageBox(
                        "سال و ماه تاریخ مبنای داده‌ها را انتخاب کنید",
                        "اعتبارسنجی",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                DialogResult result = UiMessageService.ShowMessageBox(
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

                UiMessageService.ShowMessageBox(
                    "تاریخ مبنای شروع داده‌ها با موفقیت ذخیره شد",
                    "تنظیمات",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                UpdateDataBaselineInfo();
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "FrmSettings.SaveDataStartDate");
                UiMessageService.ShowError("ذخیره تاریخ مبنای داده‌ها انجام نشد. ورودی‌ها و دسترسی داده را بررسی کنید.", "خطا");
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
            pnlBody.BackColor = palette.ContentBackColor;
            pnlFooter.BackColor = palette.ContentBackColor;

            lblTitle.ForeColor = palette.TextOnAccentColor;
            lblSubTitle.ForeColor = Color.WhiteSmoke;

            ApplyThemeToGroupBox(gbTheme);
            ApplyThemeToGroupBox(gpDatabase);
            ApplyThemeToGroupBox(gpPassword);
            ApplyThemeToGroupBox(grpRuntimeSettings);
            ApplyThemeToGroupBox(grbBaseLine);

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
            UiStyleService.ApplyButtonConventions(btnSave, palette);
            AppThemeManager.ApplyToPrimaryButton(btnSave);

            UpdateThemeRadioStyles();

            ApplySettingsLayout();

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

            groupBox.BackColor = palette.CardBackColor;
            groupBox.ForeColor = palette.TextPrimaryColor;
            groupBox.Font = UiStyleService.CreateFont(UiStyleService.SectionFontSize, FontStyle.Bold);
        }
        /// <summary>
        /// اعمال ظاهر پایه روی RadioButton.
        /// </summary>
        private static void ApplyThemeToRadioButton(RadioButton radioButton)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            radioButton.BackColor = radioButton.Parent?.BackColor ?? palette.CardBackColor;
            radioButton.ForeColor = palette.TextPrimaryColor;
            radioButton.FlatStyle = FlatStyle.Flat;
            radioButton.Font = UiStyleService.CreateFont();
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
                radioButton.Font = UiStyleService.CreateFont(9f, FontStyle.Bold);
            }
            else
            {
                radioButton.ForeColor = palette.TextPrimaryColor;
                radioButton.Font = UiStyleService.CreateFont();
            }
        }
        /// <summary>
        /// اعمال ظاهر تم روی دکمه‌های فرم تنظیمات.
        /// </summary>
        private static void ApplyThemeToSettingsButton(Button button)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            UiStyleService.ApplyButtonConventions(button, palette);
            AppThemeManager.ApplyToSecondaryButton(button);
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
            const string scope = "legacy-integrity-repair";
            if (!TryAuthorizeMaintenance(ProtectedAction.IntegrityRepair, scope,
                    out ManagementAuthorizationProof? proof, out int version)) return;
            try
            {
                DatabaseMaintenanceService.RepairIndexes(proof!, version);
                UiMessageService.ShowInfo("تعمیر و نگهداری دیتابیس با موفقیت انجام شد.", "تعمیر و نگهداری");
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "FrmSettings.RepairDatabase");
                UiMessageService.ShowError("تعمیر و نگهداری دیتابیس انجام نشد.", "خطا");
            }
        }
        /// <summary>
        /// خروجی گرفتن از فایل دیتابیس در مسیر انتخاب‌شده.
        /// </summary>
        private void btnExportDatabase_Click(object? sender, EventArgs e)
        {
            if (!QualificationManagementAccess.TryGetSession(out QualificationManagementAccess.QualificationSession? session, out _))
            {
                ShowProtectedMaintenanceUnavailable();
                return;
            }

            using SaveFileDialog dialog = new()
            {
                Filter = "نسخه پشتیبان رمزنگاری‌شده (*.rnbk)|*.rnbk",
                DefaultExt = "rnbk",
                AddExtension = true,
                InitialDirectory = ApplicationDataPaths.Default.BackupsDirectory,
                FileName = $"RahNegar_Qualification_{DateTime.Now:yyyyMMdd_HHmmss}.rnbk",
                Title = "انتخاب محل پشتیبان‌گیری"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (!IsWithinQualificationRoot(session!.DataRoot, dialog.FileName))
            {
                UiMessageService.ShowWarning("مقصد باید داخل مسیر داده جداشده باشد.", "مسیر نامعتبر");
                return;
            }

            string destination = Path.GetFullPath(dialog.FileName);
            string scope = SqliteProtectedActionBinding.CreateBackupScope(
                SqliteDatabaseHelper.GetDatabasePath(), destination, BackupOverwritePolicy.Deny);
            if (!TryAuthorizeMaintenance(ProtectedAction.BackupPolicy, scope,
                    out ManagementAuthorizationProof? proof, out int version)) return;
            if (UiMessageService.ShowMessageBox("نسخه پشتیبان ایجاد می‌شود. ادامه می‌دهید؟", "تأیید پشتیبان‌گیری",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                DatabaseMaintenanceService.ExportDatabase(destination, proof!, version);
                UiMessageService.ShowInfo("پشتیبان‌گیری با موفقیت انجام شد.", "پشتیبان‌گیری");
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "FrmSettings.ExportDatabase");
                UiMessageService.ShowError("پشتیبان‌گیری انجام نشد.", "خطا");
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
            if (!QualificationManagementAccess.TryGetSession(out QualificationManagementAccess.QualificationSession? session, out _))
            {
                ShowProtectedMaintenanceUnavailable();
                return;
            }

            using OpenFileDialog dialog = new()
            {
                Filter = "نسخه پشتیبان رمزنگاری‌شده (*.rnbk)|*.rnbk|همه فایل‌ها (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = ApplicationDataPaths.Default.BackupsDirectory,
                Title = "انتخاب نسخه پشتیبان برای بازیابی"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (!IsWithinQualificationRoot(session!.DataRoot, dialog.FileName))
            {
                UiMessageService.ShowWarning("نسخه پشتیبان باید داخل مسیر داده جداشده باشد.", "مسیر نامعتبر");
                return;
            }

            string backup = Path.GetFullPath(dialog.FileName);
            string databasePath = SqliteDatabaseHelper.GetDatabasePath();
            string rollback = DatabaseMaintenanceService.CreateRestoreRollbackPath(databasePath);
            string checksum = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(backup)));
            string scope = SqliteProtectedActionBinding.CreateRestoreScope(
                backup, checksum, databasePath, rollback);
            if (!TryAuthorizeMaintenance(ProtectedAction.Restore, scope,
                    out ManagementAuthorizationProof? proof, out int version)) return;
            if (UiMessageService.ShowMessageBox("بازیابی نسخه پشتیبان، داده‌های جداشده فعلی را جایگزین می‌کند. ادامه می‌دهید؟",
                    "تأیید بازیابی", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                DatabaseMaintenanceService.ImportDatabase(backup, rollback, proof!, version);
                UiMessageService.ShowInfo("بازیابی با موفقیت انجام شد.", "بازیابی");
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "FrmSettings.ImportDatabase");
                UiMessageService.ShowError("بازیابی انجام نشد.", "خطا");
            }
        }

        // ================= Security Methods =================

        private static void ShowProtectedMaintenanceUnavailable()
        {
            UiMessageService.ShowWarning(
                "این عملیات تا زمان فراهم بودن مجوز مدیریتی و ثبت ممیزی مدیریت‌شده در دسترس نیست.",
                "دسترسی ایمن در دسترس نیست");
        }

        private static bool TryAuthorizeMaintenance(ProtectedAction action, string scope,
            out ManagementAuthorizationProof? proof, out int credentialVersion)
        {
            proof = null;
            credentialVersion = 0;
            if (!QualificationManagementAccess.IsEnabled(out _))
            {
                ShowProtectedMaintenanceUnavailable();
                return false;
            }

            QualificationManagementAccess.TryGetSyntheticCredentialForInspection(out string initialCredential);
            using FrmPasswordConfirm dialog = new(initialCredential);
            if (dialog.ShowDialog() != DialogResult.OK) return false;

            if (QualificationManagementAccess.TryAuthorize(action, scope, Guid.NewGuid().ToString("N"),
                    dialog.Password.AsMemory(), out proof, out credentialVersion, out _)) return true;

            UiMessageService.ShowWarning("مجوز مدیریتی پذیرفته نشد.", "دسترسی رد شد");
            return false;
        }

        private static bool IsWithinQualificationRoot(string root, string candidate)
        {
            string parent = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string value = Path.GetFullPath(candidate);
            return value.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }
        private void btnResetFactory_Click(object sender, EventArgs e)
        {
            ShowProtectedMaintenanceUnavailable();
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
            ShowProtectedMaintenanceUnavailable();
        }

    }
}

