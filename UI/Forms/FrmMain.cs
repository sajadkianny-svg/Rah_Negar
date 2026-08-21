using Rah_Negar.Core;
using Rah_Negar.Data;
using Rah_Negar.Services;
using Rah_Negar.Services.Reports;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Text;
using Rah_Negar.UI.Forms.Base;

namespace Rah_Negar.UI.Forms
{
    public partial class FrmMain : BaseForm
    {
        // ================= Fields =================

        /// <summary>
        /// شماره تم فعال برنامه.
        /// </summary>
        private int _currentThemeIndex;


        /// <summary>
        /// شماره آخرین تم اعمال‌شده روی فرم.
        /// </summary>
        private int _appliedThemeIndex = -1;

        // ================= Constructor =================

        public FrmMain()
        {
            InitializeComponent();
            pnlFooter.Resize += (_, _) => PositionStatusLabel();

            KeyPreview = true;
            KeyDown += Frm_KeyDown;

            LoadSavedTheme();
            ApplyThemeToMainForm();
            WireCardHover();
            WireCardClicks();
            SetStatusText();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!AppSession.IsLoggedIn)
            {
                MessageBox.Show(
                    "دسترسی غیرمجاز",
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                Close();
            }
        }

        // ================= Theme Methods =================

        /// <summary>
        /// تم ذخیره‌شده در دیتابیس را بارگذاری می‌کند.
        /// </summary>
        private void LoadSavedTheme()
        {
            var settings = AppSettingsService.GetSettings();

            _currentThemeIndex = settings?.ThemeIndex ?? 0;

            AppThemeManager.LoadThemeByIndex(_currentThemeIndex);
        }

        /// <summary>
        /// اعمال تم فعال روی فرم اصلی.
        /// کارت‌ها تیره‌ترین رنگ تم را می‌گیرند و فوتر از کارت‌ها تیره‌تر می‌شود.
        /// </summary>
        private void ApplyThemeToMainForm()
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            Color cardColor = GetDarkestThemeColor(palette);
            Color footerColor = ControlPaint.Dark(cardColor, 0.20f);

            BackColor = palette.FormBackColor;

            //pnlHeader.BackColor = palette.HeaderBackColor;
            pnlBody.BackColor = palette.ContentBackColor;
            pnlFooter.BackColor = footerColor;
            picLogo.BackColor = pnlBody.BackColor;

            ConfigureStatusLabel(footerColor);

            ApplyCardStyle(cardRecords, isHover: false);
            ApplyCardStyle(cardReports, isHover: false);
            ApplyCardStyle(cardSettings, isHover: false);

            ConfigureCardLabels();
            _appliedThemeIndex = _currentThemeIndex;
        }

        /// <summary>
        /// تیره‌ترین رنگ قابل استفاده را از بین رنگ‌های اصلی تم پیدا می‌کند.
        /// </summary>
        private static Color GetDarkestThemeColor(AppThemePalette palette)
        {
            Color[] colors =
            [
                palette.FormBackColor,
                palette.HeaderBackColor,
                palette.ContentBackColor,
                palette.CardBackColor,
                palette.NavigationInactiveBackColor,
                palette.GridFixedCellBackColor,
                palette.PrimaryButtonDownColor
            ];

            return colors
                .OrderBy(GetBrightness)
                .First();
        }

        /// <summary>
        /// روشنایی تقریبی رنگ را محاسبه می‌کند.
        /// عدد کمتر یعنی رنگ تیره‌تر.
        /// </summary>
        private static double GetBrightness(Color color)
        {
            return (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255;
        }

        /// <summary>
        /// رنگ متن خوانا را بر اساس رنگ پس‌زمینه برمی‌گرداند.
        /// </summary>
        private static Color GetReadableTextColor(Color backColor)
        {
            return GetBrightness(backColor) < 0.55
                ? Color.White
                : Color.FromArgb(35, 35, 35);
        }


        // ================= Card Methods =================

        /// <summary>
        /// تنظیم ظاهر و موقعیت لیبل‌های کارت‌ها.
        /// </summary>
        private void ConfigureCardLabels()
        {
            ConfigureSingleLabel(lblRecordsTitle, "📝 Data Entry", cardRecords.BackColor);
            ConfigureSingleLabel(lblReportsTitle, "📊 Report Center", cardReports.BackColor);
            ConfigureSingleLabel(lblSettingsTitle, "⚙ Setting", cardSettings.BackColor);
        }

        /// <summary>
        /// تنظیم ظاهر یک لیبل داخل کارت.
        /// </summary>
        private static void ConfigureSingleLabel(Label label, string text, Color cardBackColor)
        {
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            label.ForeColor = GetReadableTextColor(cardBackColor);
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            label.Cursor = Cursors.Hand;
        }

        /// <summary>
        /// اعمال ظاهر کارت‌های اصلی فرم Main.
        /// </summary>
        private void ApplyCardStyle(Panel card, bool isHover)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            Color baseCardColor = GetDarkestThemeColor(palette);

            card.BackColor = isHover
                ? ControlPaint.Light(baseCardColor, 0.18f)
                : baseCardColor;

            card.BorderStyle = BorderStyle.None;
            card.Cursor = Cursors.Hand;
        }

        /// <summary>
        /// اتصال افکت Hover به کارت‌ها.
        /// </summary>
        private void WireCardHover()
        {
            WireSingleCardHover(cardRecords, lblRecordsTitle);
            WireSingleCardHover(cardReports, lblReportsTitle);
            WireSingleCardHover(cardSettings, lblSettingsTitle);
        }

        private void WireSingleCardHover(Panel card, Label label)
        {
            card.MouseEnter += (_, _) => SetCardHover(card, label, true);
            card.MouseLeave += (_, _) => SetCardHover(card, label, false);

            label.MouseEnter += (_, _) => SetCardHover(card, label, true);
            label.MouseLeave += (_, _) => SetCardHover(card, label, false);
        }

        private void SetCardHover(Panel card, Label label, bool isHover)
        {
            ApplyCardStyle(card, isHover);

            label.ForeColor = isHover
                ? AppThemeManager.CurrentPalette.TextOnAccentColor
                : GetReadableTextColor(card.BackColor);
        }

        // ================= Status Bar Methods =================
        /// <summary>
        /// موقعیت لیبل وضعیت را طوری تنظیم می‌کند که ابتدای متن فارسی از سمت راست فوتر شروع شود.
        /// </summary>
        private void PositionStatusLabel()
        {
            const int rightPadding = 0;

            lblStatus.AutoSize = false;
            lblStatus.RightToLeft = RightToLeft.Yes;
            lblStatus.TextAlign = ContentAlignment.MiddleRight;

            Size textSize = TextRenderer.MeasureText(
                lblStatus.Text,
                lblStatus.Font,
                new Size(int.MaxValue, pnlFooter.Height),
                TextFormatFlags.RightToLeft | TextFormatFlags.SingleLine);

            int labelWidth = Math.Min(textSize.Width + 10, pnlFooter.ClientSize.Width - rightPadding);

            lblStatus.Width = labelWidth;
            lblStatus.Height = pnlFooter.ClientSize.Height;

            lblStatus.Left = pnlFooter.ClientSize.Width - labelWidth - rightPadding;
            lblStatus.Top = 0;
        }
        /// <summary>
        /// تنظیم ظاهر لیبل وضعیت در فوتر.
        /// محل متن از ابتدای سمت راست فوتر شروع می‌شود.
        /// </summary>
        private void ConfigureStatusLabel(Color footerBackColor)
        {
            lblStatus.RightToLeft = RightToLeft.Yes;
            lblStatus.TextAlign = ContentAlignment.MiddleRight;
            lblStatus.Dock = DockStyle.None;
            lblStatus.AutoSize = false;
            // lblStatus.Padding = new Padding(0, 0, 5, 0);
            lblStatus.Font = new Font("Tahoma", 8F, FontStyle.Regular);
            lblStatus.ForeColor = GetReadableTextColor(footerBackColor);
        }

        /// <summary>
        /// تولید متن نوار وضعیت پایین فرم.
        /// </summary>
        private void SetStatusText()
        {
            AppSettingsModel? settings = AppSettingsService.GetSettings();

            StringBuilder sb = new();

            string stationName = settings?.StationName ?? "Unknown";
            string stationFa = GetPersianStationName(stationName);

            string today = GetPersianSystemDate();
            string dayName = GetPersianDayName();

            bool isDbOk = TryGetLastRecordedDateRep(out long? lastDateRep);

            string lastRecord = lastDateRep.HasValue
                ? FormatPersianDate(lastDateRep.Value)
                : "بدون داده";

            sb.Append(isDbOk ? "● آنلاین" : "● خطا در دیتابیس");

            sb.Append($"  |  {stationFa}");
            sb.Append($"  |  امروز: {dayName} {today}");
            sb.Append($"  |  آخرین ثبت: {lastRecord}");

            string? finalMsg = MonthlyFinalizeStatusService.GetPendingFinalReportMessage();

            if (!string.IsNullOrWhiteSpace(finalMsg))
                sb.Append(" | " + finalMsg);

            lblStatus.Text = ToPersianDigits(sb.ToString());

            if (!isDbOk)
                lblStatus.ForeColor = Color.Red;
            PositionStatusLabel();
        }

        /// <summary>
        /// آخرین تاریخ ثبت‌شده را از جدول tbl_data می‌خواند.
        /// خروجی false یعنی خطای واقعی دیتابیس.
        /// خروجی true با مقدار null یعنی دیتابیس سالم است ولی داده ندارد.
        /// </summary>
        private static bool TryGetLastRecordedDateRep(out long? lastDateRep)
        {
            lastDateRep = null;

            try
            {
                using var conn = SqliteDatabaseHelper.CreateConnection();
                using var cmd = conn.CreateCommand();

                cmd.CommandText = "SELECT MAX(date_rep) FROM tbl_data;";

                object? result = cmd.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                    return true;

                lastDateRep = Convert.ToInt64(result);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ================= Date Helper Methods =================

        private void RefreshMainAfterChildClosed(bool forceStatusRefresh)
        {
            AppSettingsModel? settings = AppSettingsService.GetSettings();

            int savedThemeIndex = settings?.ThemeIndex ?? 6;

            if (savedThemeIndex != _appliedThemeIndex)
            {
                _currentThemeIndex = savedThemeIndex;
                AppThemeManager.LoadThemeByIndex(_currentThemeIndex);
                ApplyThemeToMainForm();

                _appliedThemeIndex = _currentThemeIndex;
            }

            if (forceStatusRefresh)
                SetStatusText();
        }

        /// <summary>
        /// تاریخ شمسی امروز سیستم را برمی‌گرداند.
        /// </summary>
        private static string GetPersianSystemDate()
        {
            PersianCalendar pc = new();
            DateTime now = DateTime.Now;

            return $"{pc.GetYear(now):0000}/{pc.GetMonth(now):00}/{pc.GetDayOfMonth(now):00}";
        }

        /// <summary>
        /// نام فارسی روز جاری را برمی‌گرداند.
        /// </summary>
        private static string GetPersianDayName()
        {
            return DateTime.Now.DayOfWeek switch
            {
                DayOfWeek.Saturday => "شنبه",
                DayOfWeek.Sunday => "یکشنبه",
                DayOfWeek.Monday => "دوشنبه",
                DayOfWeek.Tuesday => "سه‌شنبه",
                DayOfWeek.Wednesday => "چهارشنبه",
                DayOfWeek.Thursday => "پنج‌شنبه",
                DayOfWeek.Friday => "جمعه",
                _ => ""
            };
        }

        /// <summary>
        /// date_rep را به فرمت نمایشی yyyy/MM/dd تبدیل می‌کند.
        /// </summary>
        private static string FormatPersianDate(long dateRep)
        {
            string s = dateRep.ToString();

            if (s.Length != 8)
                return "-";

            return $"{s[..4]}/{s.Substring(4, 2)}/{s.Substring(6, 2)}";
        }

        /// <summary>
        /// تبدیل نام انگلیسی ایستگاه به نام فارسی.
        /// </summary>
        private static string GetPersianStationName(string? stationName)
        {
            if (string.IsNullOrWhiteSpace(stationName))
                return "نامشخص";

            return stationName.Trim() switch
            {
                "Rasht Station" => "رشت",
                "Ramsar Station" => "رامسر",
                _ => stationName
            };
        }

        /// <summary>
        /// تبدیل اعداد انگلیسی به اعداد فارسی برای نمایش در UI.
        /// </summary>
        private static string ToPersianDigits(string input)
        {
            return input
                .Replace("0", "۰")
                .Replace("1", "۱")
                .Replace("2", "۲")
                .Replace("3", "۳")
                .Replace("4", "۴")
                .Replace("5", "۵")
                .Replace("6", "۶")
                .Replace("7", "۷")
                .Replace("8", "۸")
                .Replace("9", "۹");
        }


        // ================= Navigation Methods =================

        /// <summary>
        /// اتصال کلیک کارت‌ها به فرم‌های مربوطه.
        /// </summary>
        private void WireCardClicks()
        {
            WireSingleCardClick(cardRecords, lblRecordsTitle, OpenRecords);
            WireSingleCardClick(cardReports, lblReportsTitle, OpenReports);
            WireSingleCardClick(cardSettings, lblSettingsTitle, OpenSettings);
        }

        private static void WireSingleCardClick(
            Panel card,
            Label label,
            EventHandler handler)
        {
            card.Click -= handler;
            label.Click -= handler;

            card.Click += handler;
            label.Click += handler;
        }

        private void OpenRecords(object? sender, EventArgs e)
        {
            using FrmRecords frm = new();

            Hide();
            frm.ShowDialog(this);

            Show();
            Activate();
            RefreshMainAfterChildClosed(forceStatusRefresh: false);
        }

        private void OpenReports(object? sender, EventArgs e)
        {
            using FrmReportCenter frm = new();

            Hide();
            frm.ShowDialog(this);

            Show();
            Activate();
            RefreshMainAfterChildClosed(forceStatusRefresh: false);
        }

        private void OpenSettings(object? sender, EventArgs e)
        {
            using FrmSettings frm = new();

            Hide();
            frm.ShowDialog(this);

            Show();
            Activate();

            RefreshMainAfterChildClosed(forceStatusRefresh: false);
        }

        // ================= Shortcut Methods =================

        /// <summary>
        /// مدیریت کلیدهای ترکیبی تغییر و ریست تم.
        /// </summary>
        private void Frm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.T)
            {
                _currentThemeIndex = AppThemeManager.LoadNextTheme(_currentThemeIndex);
                AppSettingsService.SaveThemeIndex(_currentThemeIndex);

                ApplyThemeToMainForm();
                SetStatusText();

                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.Shift && e.KeyCode == Keys.R)
            {
                _currentThemeIndex = 6;
                AppThemeManager.LoadThemeByIndex(_currentThemeIndex);
                AppSettingsService.SaveThemeIndex(_currentThemeIndex);

                ApplyThemeToMainForm();
                SetStatusText();

                e.SuppressKeyPress = true;
            }
        }

    }
}