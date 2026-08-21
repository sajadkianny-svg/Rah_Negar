using Microsoft.Data.Sqlite;
using Rah_Negar.Core;
using Rah_Negar.Core.Reports;
using Rah_Negar.Data;
using Rah_Negar.Models.Reports;
using Rah_Negar.Services;
using Rah_Negar.Services.Reports;
using Rah_Negar.Services.UI;
using Rah_Negar.UI.Forms.Base;
using Rah_Negar.Utils;
using System.Data;
using System.Runtime.InteropServices;

namespace Rah_Negar.UI.Forms
{

    public partial class FrmReportCenter : BaseForm
    {
        #region Fields
        //0-Fields================================================================================================

        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, bool wParam, IntPtr lParam);

        private string _stationName = string.Empty;
        private long _dataStartDate;

        private ReportRequest? _currentGeneratedRequest;
        private ReportResult? _currentGeneratedReportResult;

        #endregion

        #region Initialization
        //1-Initialization========================================================================================

        /// <summary>
        /// پروفایل گزارش ایستگاه فعال.
        /// شامل تعریف پارامترها، ساختار گزارش و قوانین محاسباتی وابسته به ایستگاه.
        /// </summary>
        private ReportStationProfile _reportProfile = null!;
        /// <summary>
        /// شماره تم فعال فرم گزارش.
        /// </summary>
        private int _currentThemeIndex;

        /// <summary>
        /// مشخص می‌کند تم کامل فرم قبلاً اعمال شده یا نه.
        /// </summary>
        private bool _isThemeApplied;

        /// <summary>
        /// شماره آخرین تم اعمال‌شده روی فرم.
        /// </summary>
        private int _appliedThemeIndex = -1;

        private int _currentRecycleChangeCount;

        private long _currentReportDateFrom;
        private long _currentReportDateTo;

        /// <summary>
        /// آخرین نتیجه گزارش رویدادها برای استفاده در لاگ و تغییر حالت نمایش.
        /// </summary>
        private EventReportResult? _currentEventReportResult;

        public FrmReportCenter()
        {
            InitializeComponent();

            KeyPreview = true;
            KeyDown += Frm_KeyDown;
        }
        private void FrmReportCenter_Load_1(object sender, EventArgs e)
        {
            InitializeReportCenter();
            LoadReportCenterSettings();
        }
        /// <summary>
        /// تنظیمات اولیه فرم گزارش را انجام می‌دهد.
        /// </summary>
        private void InitializeReportCenter()
        {
            AppSettingsModel? settings = AppSettingsService.GetSettings();

            if (settings == null || !settings.IsInitialized)
            {
                UiMessageService.ShowError("تنظیمات برنامه بارگذاری نشده است", "خطا");
                return;
            }

            rdoLogByEvent.Visible = false;
            rdoLogByUnit.Visible = false;

            // 🔴 خیلی مهم — قبل از هر استفاده
            _reportProfile = ReportStationProfileProvider.GetProfile(settings.StationName);

            _currentThemeIndex = settings.ThemeIndex;
            AppThemeManager.LoadThemeByIndex(_currentThemeIndex);

            // TODO: بعداً اینا رو وصل می‌کنیم به DB واقعی
            LoadMonths();
            LoadYears();

            ConfigureSummaryGrid();
            ConfigureExtremeDatesGrid();

            ConfigureUniqueSummaryGrid();
            ConfigureEventSummaryGrid();

            ConfigureServiceDaysGrid();
            ConfigureEventLogGrid();
            ConfigureServiceCombinationGrid();


            InitializeSummaryGridRows();
            InitializeUniqueSummaryGridRows();
            InitializeEventSummaryGridRows();

            UpdatePeriodControlsState();
            EnableFormDoubleBuffering();

            SetInitialEmptyState();

            _isThemeApplied = false;
            ApplyThemeToReportForm();

        }
        /// <summary>
        /// سال‌های موجود در جدول tbl_unique را از روی فیلد date_rep خوانده
        /// و داخل ComboBox سال بارگذاری می‌کند.
        /// </summary>
        private void LoadYears()
        {
            cmbYear.Items.Clear();

            using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText =
                """
        SELECT DISTINCT CAST(date_rep / 10000 AS INTEGER) AS year_rep
        FROM tbl_unique
        WHERE date_rep IS NOT NULL
        ORDER BY year_rep DESC;
        """;

            using SqliteDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                cmbYear.Items.Add(reader.GetInt32(0));
            }

            if (cmbYear.Items.Count > 0)
            {
                cmbYear.SelectedIndex = 0;
            }

            // مقدار پیش‌فرض (اختیاری)
            cmbYear.SelectedIndex = -1;
        }
        /// <summary>
        /// بارگذاری ماه‌های سال در ComboBox مربوطه.
        /// ترتیب ماه‌ها بر اساس تقویم شمسی تنظیم شده است.
        /// </summary>
        private void LoadMonths()
        {
            cmbMonth.Items.Clear();

            cmbMonth.Items.AddRange(new object[]
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

            // مقدار پیش‌فرض (اختیاری)
            cmbMonth.SelectedIndex = -1;
        }

        /// <summary>
        /// بارگذاری تنظیمات اصلی برنامه برای استفاده در گزارش‌گیری.
        /// </summary>
        private void LoadReportCenterSettings()
        {
            using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

            const string sql = @"
            SELECT station_name, data_start_date
            FROM app_settings
            LIMIT 1;";

            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            using SqliteDataReader reader = cmd.ExecuteReader();

            if (!reader.Read())
                throw new InvalidOperationException("تنظیمات اولیه برنامه یافت نشد.");

            _stationName = reader["station_name"].ToString() ?? string.Empty;
            _dataStartDate = Convert.ToInt64(reader["data_start_date"]);
        }

        /// <summary>
        /// فعال‌سازی DoubleBuffer روی کنترل‌های اصلی فرم برای کاهش Flicker.
        /// </summary>
        private void EnableFormDoubleBuffering()
        {
            DoubleBuffered = true;

            EnableControlDoubleBuffer(pnlContent);
            EnableControlDoubleBuffer(pnlSummaryPage);
            EnableControlDoubleBuffer(pnlEventsPage);
            EnableControlDoubleBuffer(pnlServicePage);
            EnableControlDoubleBuffer(pnlLogPage);

            EnableDataGridViewDoubleBuffer(dgvSummary);
            EnableDataGridViewDoubleBuffer(dgvUniqueSummary);
            EnableDataGridViewDoubleBuffer(dgvEventSummary);
            EnableDataGridViewDoubleBuffer(dgvServiceDays);
            EnableDataGridViewDoubleBuffer(dgvServiceCombination);
            EnableDataGridViewDoubleBuffer(dgvEventLog);
            EnableDataGridViewDoubleBuffer(dgvExtremeDates);
        }

        /// <summary>
        /// فعال‌سازی DoubleBuffer روی کنترل معمولی با Reflection.
        /// </summary>
        private static void EnableControlDoubleBuffer(Control control)
        {
            typeof(Control)
                .GetProperty(
                    "DoubleBuffered",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(control, true, null);
        }

        /// <summary>
        /// فعال‌سازی DoubleBuffer داخلی DataGridView برای کاهش Flicker.
        /// </summary>
        private static void EnableDataGridViewDoubleBuffer(DataGridView dgv)
        {
            typeof(DataGridView)
                .GetProperty(
                    "DoubleBuffered",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(dgv, true, null);
        }
        //===========================================================================================================
        #endregion

        #region Theme Methods
        //2-Theme Methods=========================================================================
        /// <summary>
        /// اعمال تم فعال روی فرم گزارش و کنترل‌های اصلی آن.
        /// </summary>
        private void ApplyThemeToReportForm()
        {
            if (_isThemeApplied && _appliedThemeIndex == _currentThemeIndex)
                return;

            AppThemePalette palette = AppThemeManager.CurrentPalette;

            BackColor = palette.FormBackColor;

            pnlHeader.BackColor = palette.HeaderBackColor;
            pnlContent.BackColor = palette.ContentBackColor;
            pnlSummaryPage.BackColor = palette.ContentBackColor;
            pnlEventsPage.BackColor = palette.ContentBackColor;
            pnlLogPage.BackColor = palette.ContentBackColor;
            pnlServicePage.BackColor = palette.ContentBackColor;
            pnlServiceTop.BackColor = palette.ContentBackColor;
            pnlServiceBottom.BackColor = palette.ContentBackColor;

            pnlLeft.BackColor = palette.ContentBackColor;
            pnlRight.BackColor = palette.ContentBackColor;
            pnlDivider.BackColor = palette.DividerBackColor;

            pnlFilterCard.BackColor = palette.CardBackColor;
            pnlNavigation.BackColor = palette.ContentBackColor;

            lblTitle.ForeColor = palette.TextOnAccentColor;

            AppThemeManager.ApplyToPrimaryButton(btnPDF);
            AppThemeManager.ApplyToPrimaryButton(btnGenerateReport);
            AppThemeManager.ApplyToPrimaryButton(btnFinalizeMonthlyReport);

            AppThemeManager.ApplyToNavigationButton(btnSummaryPage, pnlSummaryPage.Visible);
            AppThemeManager.ApplyToNavigationButton(btnEventsPage, pnlEventsPage.Visible);
            AppThemeManager.ApplyToNavigationButton(btnServicePage, pnlServicePage.Visible);
            AppThemeManager.ApplyToNavigationButton(btnLogPage, pnlLogPage.Visible);

            AppThemeManager.ApplyToReportGrid(dgvSummary);
            AppThemeManager.ApplyToReportGrid(dgvUniqueSummary);
            AppThemeManager.ApplyToReportGrid(dgvEventSummary);
            AppThemeManager.ApplyToReportGrid(dgvServiceDays);
            AppThemeManager.ApplyToReportGrid(dgvEventLog);
            AppThemeManager.ApplyToReportGrid(dgvServiceCombination);
            AppThemeManager.ApplyToReportGrid(dgvExtremeDates);

            ApplyFixedGridColumnsTheme();
            ApplyThemeToRadioButtons();

            _isThemeApplied = true;
            _appliedThemeIndex = _currentThemeIndex;

            Invalidate();
        }

        /// <summary>
        /// اعمال رنگ سلول‌های ثابت گریدهای گزارش
        /// این ستون‌ها نقش عنوان داخلی دارند و باید از سلول‌های مقداری جدا دیده شوند
        /// </summary>
        private void ApplyFixedGridColumnsTheme()
        {
            if (dgvSummary.Columns.Contains("colParameter"))
            {
                AppThemeManager.ApplyFixedCellStyle(
                    dgvSummary.Columns["colParameter"].DefaultCellStyle);
            }

            if (dgvUniqueSummary.Columns.Contains("colItem"))
            {
                AppThemeManager.ApplyFixedCellStyle(
                    dgvUniqueSummary.Columns["colItem"].DefaultCellStyle);
            }

            if (dgvEventSummary.Columns.Contains("colMetric"))
            {
                AppThemeManager.ApplyFixedCellStyle(
                    dgvEventSummary.Columns["colMetric"].DefaultCellStyle);
            }

            if (dgvServiceDays.Columns.Contains("colUnit"))
            {
                AppThemeManager.ApplyFixedCellStyle(
                    dgvServiceDays.Columns["colUnit"].DefaultCellStyle);
            }

            if (dgvServiceDays.Columns.Contains("colCombination"))
            {
                AppThemeManager.ApplyFixedCellStyle(
                    dgvServiceDays.Columns["colCombination"].DefaultCellStyle);
            }

            if (dgvServiceCombination.Columns.Contains("colCombination"))
            {
                AppThemeManager.ApplyFixedCellStyle(
                    dgvServiceCombination.Columns["colCombination"].DefaultCellStyle);
            }

            if (dgvEventLog.Columns.Contains("colGroup"))
            {
                AppThemeManager.ApplyFixedCellStyle(
                    dgvEventLog.Columns["colGroup"].DefaultCellStyle);
            }

            if (dgvExtremeDates.Columns.Contains("colParameter"))
            {
                AppThemeManager.ApplyFixedCellStyle(
                    dgvExtremeDates.Columns["colParameter"].DefaultCellStyle);
            }
        }

        /// <summary>
        /// مدیریت کلیدهای ترکیبی تغییر و ریست تم فرم گزارش.
        /// </summary>
        private void Frm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.T)
            {
                _currentThemeIndex = AppThemeManager.LoadNextTheme(_currentThemeIndex);
                AppSettingsService.SaveThemeIndex(_currentThemeIndex);

                _isThemeApplied = false;
                ApplyThemeToReportForm();

                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.Shift && e.KeyCode == Keys.R)
            {
                _currentThemeIndex = 6;
                AppThemeManager.LoadThemeByIndex(_currentThemeIndex);
                AppSettingsService.SaveThemeIndex(_currentThemeIndex);

                _isThemeApplied = false;
                ApplyThemeToReportForm();

                e.SuppressKeyPress = true;
            }
        }

        /// <summary>
        /// اعمال تم روی همه RadioButtonهای فرم گزارش.
        /// </summary>
        private void ApplyThemeToRadioButtons()
        {
            RadioButton[] radioButtons =
            [
                rdoMonthly,
        rdoFirstHalf,
        rdoSecondHalf,
        rdoYearly,
        rdoLogByUnit,
        rdoLogByEvent
            ];

            foreach (RadioButton radioButton in radioButtons)
            {
                ApplyThemeToRadioButton(radioButton);
            }

            UpdateRadioButtonCheckedStyles();
        }

        /// <summary>
        /// اعمال ظاهر پایه روی RadioButton.
        /// </summary>
        private static void ApplyThemeToRadioButton(RadioButton radioButton)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            radioButton.BackColor = radioButton.Parent?.BackColor ?? palette.ContentBackColor;
            radioButton.ForeColor = palette.TextPrimaryColor;
            radioButton.FlatStyle = FlatStyle.Flat;
            radioButton.FlatAppearance.BorderSize = 0;
            radioButton.Font = new Font("tahoma", 8.5F, FontStyle.Regular);
        }

        /// <summary>
        /// به‌روزرسانی ظاهر RadioButtonها بر اساس وضعیت انتخاب‌شده.
        /// </summary>
        private void UpdateRadioButtonCheckedStyles()
        {
            RadioButton[] radioButtons =
            [
                rdoMonthly,
                rdoFirstHalf,
                rdoSecondHalf,
                rdoYearly,
                rdoLogByUnit,
                rdoLogByEvent
            ];

            foreach (RadioButton radioButton in radioButtons)
            {
                ApplyRadioButtonCheckedStyle(radioButton);
            }
        }

        /// <summary>
        /// اعمال ظاهر انتخاب‌شده یا عادی روی یک RadioButton.
        /// </summary>
        private static void ApplyRadioButtonCheckedStyle(RadioButton radioButton)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            if (radioButton.Checked)
            {
                radioButton.ForeColor = palette.PrimaryButtonBackColor;
                radioButton.Font = new Font("tahoma", 8F, FontStyle.Bold);
            }
            else
            {
                radioButton.ForeColor = palette.TextPrimaryColor;
                radioButton.Font = new Font("tahoma", 8F, FontStyle.Regular);
            }
        }


        /// <summary>
        /// با تغییر حالت نمایش لاگ رویدادها، ظاهر RadioButtonها و گرید لاگ به‌روزرسانی می‌شود.
        /// فقط زمانی اجرا می‌شود که RadioButton جدید انتخاب شده باشد.
        /// </summary>
        private void EventLogMode_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is not RadioButton radioButton || !radioButton.Checked)
                return;

            UpdateRadioButtonCheckedStyles();

            if (_currentEventReportResult == null)
                return;

            BindEventLogGrid(_currentEventReportResult);
        }


        //===========================================================================================================
        #endregion

        #region UI Configuration
        //2-UI Configuration======================================================================

        /// <summary>
        /// ساختار و ظاهر گرید خلاصه آماری tbl_data را تنظیم می‌کند.
        /// </summary>
        private void ConfigureSummaryGrid()
        {
            ConfigureReportGridBase(dgvSummary);

            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colParameter",
                HeaderText = "Parameter",
                Width = 210,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle =
        {
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            BackColor = Color.Gainsboro,
            SelectionBackColor = Color.Gainsboro,
            SelectionForeColor = Color.Black
        }
            });

            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colMin",
                HeaderText = "Min",
                Width = 65,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colMax",
                HeaderText = "Max",
                Width = 65,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colAvg",
                HeaderText = "Avg",
                Width = 65,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvSummary.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        /// <summary>
        /// ساختار و ظاهر گرید خلاصه tbl_unique را تنظیم می‌کند.
        /// </summary>
        private void ConfigureUniqueSummaryGrid()
        {
            ConfigureReportGridBase(dgvUniqueSummary);

            dgvUniqueSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colItem",
                HeaderText = "Parameter",
                Width = 200,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle =
        {
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            BackColor = Color.Gainsboro,
            SelectionBackColor = Color.Gainsboro,
            SelectionForeColor = Color.Black
        }
            });

            dgvUniqueSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colValue",
                HeaderText = "Result",
                Width = 140,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvUniqueSummary.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        /// <summary>
        /// ایجاد ردیف‌های اولیه گرید Summary بر اساس پارامترهای پروفایل فعال.
        /// پارامترهای RPM در این گرید نمایش داده نمی‌شوند.
        /// </summary>
        private void InitializeSummaryGridRows()
        {
            dgvSummary.Rows.Clear();

            foreach (ReportParameterDefinition parameter in _reportProfile.Parameters
                .Where(p => p.DataColumnName != null)
                .Where(p => p.Category != ReportParameterCategory.RPM)
                .Where(p => p.Category != ReportParameterCategory.Status))
            {
                dgvSummary.Rows.Add(parameter.DisplayName, "", "", "");
            }

            dgvSummary.ClearSelection();
            dgvSummary.CurrentCell = null;
        }

        /// <summary>
        /// ایجاد ردیف‌های ثابت برای نمایش مقادیر tbl_unique
        /// </summary>
        private void InitializeUniqueSummaryGridRows()
        {
            dgvUniqueSummary.Rows.Clear();

            dgvUniqueSummary.Rows.Add("Gas Generator Fuel", "");
            dgvUniqueSummary.Rows.Add("Turbine Fuel", "");
            dgvUniqueSummary.Rows.Add("Total Fuel", "");

            dgvUniqueSummary.Rows.Add("Turbine Flow", "");
            dgvUniqueSummary.Rows.Add("Non-Turbine Flow", "");
            dgvUniqueSummary.Rows.Add("Total Flow", "");

            dgvUniqueSummary.Rows.Add("Vent", "");
            dgvUniqueSummary.Rows.Add("Recyle Change", "");

            dgvUniqueSummary.ClearSelection();
            dgvUniqueSummary.CurrentCell = null;
        }

        /// <summary>
        /// تنظیمات ظاهری مشترک همه DataGridView های گزارش را اعمال می‌کند.
        /// </summary>
        private static void ConfigureReportGridBase(DataGridView dgv)
        {
            dgv.Columns.Clear();
            dgv.Rows.Clear();

            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.AllowUserToResizeColumns = false;

            dgv.ReadOnly = true;
            dgv.MultiSelect = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            dgv.RowHeadersVisible = false;
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            dgv.GridColor = Color.Gainsboro;

            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            //dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.SteelBlue;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.2F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.ColumnHeadersHeight = 28;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            dgv.DefaultCellStyle.BackColor = Color.WhiteSmoke;
            dgv.DefaultCellStyle.ForeColor = Color.Black;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 8.2F, FontStyle.Regular);
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.DefaultCellStyle.SelectionBackColor = Color.WhiteSmoke;
            dgv.DefaultCellStyle.SelectionForeColor = Color.Black;

            dgv.RowTemplate.Height = 24;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            dgv.ClearSelection();
            dgv.CurrentCell = null;
        }

        /// <summary>
        /// ساختار و ظاهر گرید خلاصه رویدادها را بر اساس واحدهای پروفایل فعال تنظیم می‌کند.
        /// ستون‌های واحدها به‌صورت داینامیک ساخته می‌شوند.
        /// </summary>
        private void ConfigureEventSummaryGrid()
        {
            ConfigureReportGridBase(dgvEventSummary);

            dgvEventSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colMetric",
                HeaderText = "Metric",
                Width = 140,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    BackColor = Color.Gainsboro,
                    SelectionBackColor = Color.Gainsboro,
                    SelectionForeColor = Color.Black
                }
            });



            foreach (string unit in _reportProfile.Units)
            {
                dgvEventSummary.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = $"col{unit}",
                    HeaderText = unit,
                    Width = 80,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }

            dgvEventSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colTotal",
                HeaderText = "Total",
                Width = 95,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });

            dgvEventSummary.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            ApplyThemeToReportForm();
        }

        /// <summary>
        /// ردیف‌های ثابت گرید خلاصه رویدادها را ایجاد می‌کند.
        /// </summary>
        private void InitializeEventSummaryGridRows()
        {
            dgvEventSummary.Rows.Clear();

            dgvEventSummary.Rows.Add("Runtime Hours");
            dgvEventSummary.Rows.Add("Runtime After OH");
            dgvEventSummary.Rows.Add("Total Events");
            dgvEventSummary.Rows.Add("Start Count");
            dgvEventSummary.Rows.Add("N.S.D Count");
            dgvEventSummary.Rows.Add("E.S.D Count");
            dgvEventSummary.Rows.Add("E.S.D Extra Hours");
            dgvEventSummary.Rows.Add("Max Runtime");
            dgvEventSummary.Rows.Add("Day Start");
            dgvEventSummary.Rows.Add("Night Start");
            dgvEventSummary.Rows.Add("Day N.S.D");
            dgvEventSummary.Rows.Add("Night N.S.D");
            dgvEventSummary.Rows.Add("Day E.S.D");
            dgvEventSummary.Rows.Add("Night E.S.D");

            dgvEventSummary.ClearSelection();
            dgvEventSummary.CurrentCell = null;
        }

        /// <summary>
        /// ساختار و ظاهر گرید روزهای سرویس را به صورت چهار ستونه تنظیم می‌کند.
        /// ستون‌های اول و سوم نقش عنوان دارند و ستون‌های دوم و چهارم مقدار روزها را نمایش می‌دهند.
        /// </summary>
        private void ConfigureServiceDaysGrid()
        {
            ConfigureReportGridBase(dgvServiceDays);

            dgvServiceDays.Columns.Clear();

            dgvServiceDays.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUnit",
                HeaderText = "Unit",
                Width = 120,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle =
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            BackColor = Color.Gainsboro,
            SelectionBackColor = Color.Gainsboro,
            SelectionForeColor = Color.Black
        }
            });

            dgvServiceDays.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUnitDays",
                HeaderText = "day(s)",
                Width = 80,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvServiceDays.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCombination",
                HeaderText = "Combination",
                Width = 150,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle =
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            BackColor = Color.Gainsboro,
            SelectionBackColor = Color.Gainsboro,
            SelectionForeColor = Color.Black
        }
            });

            dgvServiceDays.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCombinationDays",
                HeaderText = "day(s)",
                Width = 80,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvServiceDays.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            ApplyThemeToReportForm();
        }

        /// <summary>
        /// ساختار و ظاهر گرید لاگ رویدادها را تنظیم می‌کند.
        /// </summary>
        private void ConfigureEventLogGrid()
        {
            ConfigureReportGridBase(dgvEventLog);

            dgvEventLog.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colGroup",
                HeaderText = "Group",
                Width = 105,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvEventLog.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDate",
                HeaderText = "Date",
                Width = 105,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvEventLog.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colItem",
                HeaderText = "Item",
                Width = 90,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvEventLog.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colTime",
                HeaderText = "Time",
                Width = 90,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvEventLog.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colRemark",
                HeaderText = "Remark",
                Width = 355,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvEventLog.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        }

        /// <summary>
        /// ساختار و ظاهر گرید ترکیب‌های روزهای سرویس را تنظیم می‌کند.
        /// این گرید نشان می‌دهد در هر تاریخ، چند واحد و دقیقاً کدام واحدها همزمان در سرویس بوده‌اند.
        /// </summary>
        private void ConfigureServiceCombinationGrid()
        {
            ConfigureReportGridBase(dgvServiceCombination);

            dgvServiceCombination.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCombination",
                HeaderText = "Combination",
                Width = 130,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvServiceCombination.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDate",
                HeaderText = "Date",
                Width = 130,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvServiceCombination.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUnits",
                HeaderText = "Units In Service",
                Width = 180,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvServiceCombination.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        /// <summary>
        /// ساختار و ظاهر گرید لاگ تاریخ‌های ثبت حداقل و حداکثر پارامترها را تنظیم می‌کند.
        /// این گرید به صورت لاگ‌محور طراحی شده تا اگر یک مقدار Min یا Max در چندین تاریخ رخ داد،

        private void ConfigureExtremeDatesGrid()
        {
            ConfigureReportGridBase(dgvExtremeDates);

            dgvExtremeDates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colGroup",
                HeaderText = "Group",
                Width = 110,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvExtremeDates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDate",
                HeaderText = "Date",
                Width = 85,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvExtremeDates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colType",
                HeaderText = "Type",
                Width = 50,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvExtremeDates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colValue",
                HeaderText = "Value",
                Width = 50,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            // کنترل کامل عرض ستون‌ها با کد
            dgvExtremeDates.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // ترتیب نمایش ستون‌ها
            dgvExtremeDates.Columns["colGroup"].DisplayIndex = 0;
            dgvExtremeDates.Columns["colDate"].DisplayIndex = 1;
            dgvExtremeDates.Columns["colType"].DisplayIndex = 2;
            dgvExtremeDates.Columns["colValue"].DisplayIndex = 3;

            // اجازه جابه‌جایی دستی ستون‌ها توسط کاربر
            //dgvExtremeDates.AllowUserToOrderColumns = true;

            dgvExtremeDates.ClearSelection();
            dgvExtremeDates.CurrentCell = null;
        }

        //==========================================================================================================
        #endregion

        #region Period Methods
        //3-Period Methods==========================================================================================

        /// <summary>
        /// با تغییر نوع گزارش (RadioButtonها)، وضعیت کنترل‌ها به‌روزرسانی می‌شود.
        /// </summary>
        private void ReportMode_CheckedChanged(object? sender, EventArgs e)
        {
            UpdatePeriodControlsState();
            UpdateRadioButtonCheckedStyles();

            if (_currentEventReportResult != null)
            {
                BindEventLogGrid(_currentEventReportResult);
            }
            ClearGeneratedReportCache();
            UpdateReportActionButtonsState();
        }

        /// <summary>
        /// تعیین می‌کند که آیا انتخاب ماه فعال باشد یا نه.
        /// فقط در حالت Monthly فعال است.
        /// </summary>
        private void UpdatePeriodControlsState()
        {
            cmbYear.Enabled = true;

            cmbMonth.Enabled = rdoMonthly.Checked;
            UpdateReportActionButtonsState();
        }

        /// <summary>
        /// سال و لیست ماه‌های انتخاب‌شده توسط کاربر را استخراج می‌کند.
        /// در صورت نامعتبر بودن ورودی، پیام مناسب نمایش می‌دهد.
        /// </summary>
        private bool TryGetSelectedPeriod(out int year, out List<int> months)
        {
            year = 0;
            months = new List<int>();

            if (cmbYear.SelectedItem == null)
            {
                UiMessageService.ShowWarning("لطفاً سال را انتخاب کنید", "اعتبارسنجی");
                return false;
            }

            year = Convert.ToInt32(cmbYear.SelectedItem);

            if (rdoMonthly.Checked)
            {
                if (cmbMonth.SelectedIndex < 0)
                {
                    UiMessageService.ShowWarning("لطفاً ماه را انتخاب کنید", "اعتبارسنجی");
                    return false;
                }

                if (!TryGetSelectedMonthNumber(out int selectedMonth))
                {
                    UiMessageService.ShowWarning("لطفاً ماه را انتخاب کنید", "اعتبارسنجی");
                    return false;
                }

                months.Add(selectedMonth);
            }
            else if (rdoFirstHalf.Checked)
            {
                months = new List<int> { 1, 2, 3, 4, 5, 6 };
            }
            else if (rdoSecondHalf.Checked)
            {
                months = new List<int> { 7, 8, 9, 10, 11, 12 };
            }
            else if (rdoYearly.Checked)
            {
                months = Enumerable.Range(1, 12).ToList();
            }
            else
            {
                UiMessageService.ShowWarning("نوع گزارش انتخاب نشده است", "اعتبارسنجی");
                return false;
            }

            return true;
        }

        /// <summary>
        /// بر اساس سال و لیست ماه‌ها، تاریخ شروع و پایان بازه گزارش را
        /// در قالب عددی yyyyMMdd محاسبه می‌کند.
        /// </summary>
        private static void GetDateRange(int year, List<int> months, out long startDate, out long endDate)
        {
            int startMonth = months.Min();
            int endMonth = months.Max();

            int endDay = GetPersianMonthDays(year, endMonth);

            startDate = year * 10000L + startMonth * 100L + 1;
            endDate = year * 10000L + endMonth * 100L + endDay;
        }

        private void cmbYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            ClearGeneratedReportCache();
            UpdateReportActionButtonsState();
        }

        private void cmbMonth_SelectedIndexChanged(object sender, EventArgs e)
        {
            ClearGeneratedReportCache();
            UpdateReportActionButtonsState();
        }

        //==========================================================================================================

        //4-Navigation Methods======================================================================================
        /// <summary>
        /// صفحه فعال گزارش را مشخص می‌کند و ظاهر دکمه‌های ناوبری را به‌روزرسانی می‌کند.
        /// </summary>
        private void SetActivePage(Button activeButton)
        {
            pnlSummaryPage.Visible = activeButton == btnSummaryPage;
            pnlEventsPage.Visible = activeButton == btnEventsPage;
            pnlServicePage.Visible = activeButton == btnServicePage;
            pnlLogPage.Visible = activeButton == btnLogPage;

            AppThemeManager.ApplyToNavigationButton(btnSummaryPage, activeButton == btnSummaryPage);
            AppThemeManager.ApplyToNavigationButton(btnEventsPage, activeButton == btnEventsPage);
            AppThemeManager.ApplyToNavigationButton(btnServicePage, activeButton == btnServicePage);
            AppThemeManager.ApplyToNavigationButton(btnLogPage, activeButton == btnLogPage);
        }

        /// <summary>
        /// فرم گزارش را به حالت اولیه و خالی برمی‌گرداند.
        /// تا قبل از تولید موفق گزارش، صفحات و دکمه‌های گزارش مخفی می‌مانند.
        /// </summary>
        private void SetInitialEmptyState()
        {
            pnlSummaryPage.Visible = false;
            pnlEventsPage.Visible = false;
            pnlServicePage.Visible = false;
            pnlLogPage.Visible = false;

            btnSummaryPage.Visible = false;
            btnEventsPage.Visible = false;
            btnServicePage.Visible = false;
            btnLogPage.Visible = false;

            rdoLogByUnit.Visible = false;
            rdoLogByEvent.Visible = false;
        }

        /// <summary>
        /// بعد از تولید موفق گزارش، دکمه‌های ناوبری را نمایش می‌دهد
        /// و صفحه Overview را فعال می‌کند.
        /// </summary>
        private void ShowReportPagesAfterGenerate()
        {
            btnSummaryPage.Visible = true;
            btnEventsPage.Visible = true;
            btnServicePage.Visible = true;
            btnLogPage.Visible = true;

            SetActivePage(btnSummaryPage);
        }

        //==========================================================================================================
        #endregion

        #region Report Methods
        //5-Report Methods==========================================================================================


        /// <summary>
        /// گزارش چندماهه‌ای را که تمام ماه‌های آن نهایی شده‌اند نمایش می‌دهد.
        /// اعداد اصلی از داده‌های نهایی ماهانه تجمیع می‌شوند
        /// و بخش‌های تحلیلی دوباره از داده خام محاسبه می‌شوند.
        /// </summary>
        private void LoadFinalizedPeriodReportFromSnapshot(
            int year,
            List<int> months,
            ReportRequest request)
        {
            using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

            ReportResult snapshotResult =
                PeriodFinalReportReadService.LoadPeriodSummarySnapshot(conn, year, months);

            EventReportResult eventResult =
                EventReportEngineService.BuildEventReport(
                    conn,
                    _reportProfile,
                    request.DateFrom,
                    request.DateTo);

            List<ExtremeDateItem> extremeDates =
                ExtremeDatesService.Calculate(
                    conn,
                    _reportProfile,
                    request.DateFrom,
                    request.DateTo);

            BeginFormUpdate();

            try
            {
                BindSummaryGrid(snapshotResult);
                BindUniqueSummaryGrid(snapshotResult);

                BindEventSummaryGrid(eventResult);
                BindServiceDaysGrid(eventResult);

                _currentEventReportResult = eventResult;
                BindEventLogGrid(eventResult);

                BindServiceCombinationGrid(eventResult);
                BindExtremeDatesGrid(extremeDates);

                ShowReportPagesAfterGenerate();

                //btnFinalizeMonthlyReport.Enabled = false;
            }
            finally
            {
                EndFormUpdate();
            }
        }

        /// <summary>
        /// گزارش ماهانه قفل‌شده را نمایش می‌دهد.
        /// اعداد اصلی از داده‌های نهایی ذخیره‌شده خوانده می‌شوند
        /// و بخش‌های تحلیلی دوباره محاسبه می‌شوند.
        /// </summary>
        private void LoadFinalizedMonthlyReportFromSnapshot(int year, int month)
        {
            using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

            _currentRecycleChangeCount =
                MonthlyFinalReportReadService.LoadRecycleChangeCount(conn, year, month);

            ReportResult snapshotResult =
                MonthlyFinalReportReadService.LoadMonthlySummarySnapshot(conn, year, month);

            EventReportResult eventSnapshotResult =
                MonthlyFinalReportReadService.LoadMonthlyEventSummarySnapshot(conn, year, month);

            Dictionary<string, double> serviceDaysMap =
                MonthlyFinalReportReadService.LoadServiceDaysSummary(conn, year, month);

            EventReportResult analysisEventResult =
                EventReportEngineService.BuildEventReport(
                    conn,
                    _reportProfile,
                    _currentReportDateFrom,
                    _currentReportDateTo);

            List<ExtremeDateItem> extremeDates =
                ExtremeDatesService.Calculate(
                    conn,
                    _reportProfile,
                    _currentReportDateFrom,
                    _currentReportDateTo);

            BeginFormUpdate();

            try
            {
                BindSummaryGrid(snapshotResult);
                BindUniqueSummaryGrid(snapshotResult);
                BindEventSummaryGrid(eventSnapshotResult);
                BindServiceDaysGridFromSnapshot(serviceDaysMap);

                _currentEventReportResult = analysisEventResult;
                BindEventLogGrid(analysisEventResult);

                BindServiceCombinationGrid(analysisEventResult);
                BindExtremeDatesGrid(extremeDates);

                ShowReportPagesAfterGenerate();

                //btnFinalizeMonthlyReport.Enabled = false;
            }
            finally
            {
                EndFormUpdate();
            }
        }

        /// <summary>
        /// بر اساس انتخاب‌های کاربر، درخواست گزارش‌گیری را برای موتور گزارش می‌سازد.
        /// </summary>
        private bool TryBuildReportRequest(out ReportRequest request)
        {
            request = new ReportRequest();

            if (!TryGetSelectedPeriod(out int year, out List<int> months))
                return false;

            GetDateRange(year, months, out long startDate, out long endDate);

            long dataStartDate = AppSettingsService.GetDataStartDate();

            if (startDate < dataStartDate)
            {
                UiMessageService.ShowWarning(
                    UiMessageService.Paragraphs(
                        "بازه انتخاب‌شده قبل از تاریخ مجاز شروع ثبت اطلاعات است.",
                        "اولین تاریخ مجاز:" +
                        Environment.NewLine +
                        DateFormatHelper.FormatDateRep(dataStartDate)),
                    "بازه غیرمجاز");

                return false;
            }

            request.DateFrom = startDate;
            request.DateTo = endDate;
            request.IncludeEvents = true;
            request.IncludeMissingDays = true;

            if (rdoMonthly.Checked)
                request.Granularity = ReportGranularity.Monthly;
            else if (rdoYearly.Checked)
                request.Granularity = ReportGranularity.Yearly;
            else
                request.Granularity = ReportGranularity.CustomRange;

            foreach (ReportParameterDefinition parameter in _reportProfile.Parameters)
            {
                request.SelectedParameters.Add(parameter.Key);
            }

            return true;
        }

        /// <summary>
        /// تولید گزارش‌ها به صورت async برای جلوگیری از قفل شدن فرم.
        /// در صورت ناقص بودن داده‌ها، فقط با نگه داشتن کلید Shift گزارش تولید می‌شود.
        /// </summary>
        private async void btnGenerateReport_Click(object? sender, EventArgs e)
        {
            bool formUpdateStarted = false;

            try
            {
                btnGenerateReport.Enabled = false;
                btnGenerateReport.Text = "Generating...";

                ClearGeneratedReportCache();

                if (!TryBuildReportRequest(out ReportRequest request))
                    return;

                _currentReportDateFrom = request.DateFrom;
                _currentReportDateTo = request.DateTo;

                if (TryGetSelectedPeriod(out int selectedYear, out List<int> selectedMonths))
                {
                    bool isMonthlyReport = request.Granularity == ReportGranularity.Monthly;

                    bool isPeriodReport =
                        request.Granularity == ReportGranularity.CustomRange ||
                        request.Granularity == ReportGranularity.Yearly;

                    if (isMonthlyReport && selectedMonths.Count == 1)
                    {
                        int month = selectedMonths[0];

                        if (MonthlyLockService.IsMonthLocked(selectedYear, month))
                        {
                            LoadFinalizedMonthlyReportFromSnapshot(selectedYear, month);
                            return;
                        }
                    }

                    if (isPeriodReport &&
                        MonthlyLockService.AreAllMonthsLocked(selectedYear, selectedMonths))
                    {
                        LoadFinalizedPeriodReportFromSnapshot(selectedYear, selectedMonths, request);
                        return;
                    }
                }

                bool ignoreIncompleteData = (ModifierKeys & Keys.Shift) == Keys.Shift;

                ReportGenerationBundle bundle = await Task.Run(() =>
                {
                    using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

                    ReportResult reportResult = ReportEngineService.BuildReport(
                        conn,
                        _reportProfile.StationName,
                        request);

                    bool hasIncompleteDays = HasIncompleteDays(reportResult);

                    if (hasIncompleteDays && !ignoreIncompleteData)
                    {
                        return new ReportGenerationBundle
                        {
                            ReportResult = reportResult,
                            EventReportResult = null,
                            ExtremeDateItems = [],
                            RecycleChangeCount = 0,
                            HasIncompleteDays = true,
                            WasOverridden = false
                        };
                    }

                    EventReportResult eventReportResult =
                        EventReportEngineService.BuildEventReport(
                            conn,
                            _reportProfile,
                            request.DateFrom,
                            request.DateTo);

                    List<ExtremeDateItem> extremeDateItems =
                        ExtremeDatesService.Calculate(
                            conn,
                            _reportProfile,
                            request.DateFrom,
                            request.DateTo);

                    int recycleChangeCount =
                        CalculateRecycleChanges(
                            conn,
                            request.DateFrom,
                            request.DateTo);

                    return new ReportGenerationBundle
                    {
                        ReportResult = reportResult,
                        EventReportResult = eventReportResult,
                        ExtremeDateItems = extremeDateItems,
                        RecycleChangeCount = recycleChangeCount,
                        HasIncompleteDays = hasIncompleteDays,
                        WasOverridden = hasIncompleteDays && ignoreIncompleteData
                    };
                });

                if (bundle.HasIncompleteDays && !bundle.WasOverridden)
                {
                    UiMessageService.ShowWarning("داده‌های بازه انتخابی ناقص است", "اعتبارسنجی");

                    SetInitialEmptyState();
                    return;
                }

                if (bundle.WasOverridden)
                {
                    UiMessageService.ShowWarning("گزارش با وجود ناقص بودن داده‌های بازه انتخابی تولید می‌شود" +
                        Environment.NewLine +
                        Environment.NewLine +
                        "نتایج این گزارش ممکن است کامل یا قابل استناد نهایی نباشد", "اعتبارسنجی");
                }

                if (bundle.EventReportResult == null)
                    return;

                BeginFormUpdate();
                formUpdateStarted = true;

                _currentGeneratedRequest = request;
                _currentGeneratedReportResult = bundle.ReportResult;
                _currentEventReportResult = bundle.EventReportResult;
                _currentRecycleChangeCount = bundle.RecycleChangeCount;

                BindSummaryGrid(bundle.ReportResult);
                BindExtremeDatesGrid(bundle.ExtremeDateItems);
                BindUniqueSummaryGrid(bundle.ReportResult);

                BindEventSummaryGrid(bundle.EventReportResult);
                BindServiceDaysGrid(bundle.EventReportResult);
                BindServiceCombinationGrid(bundle.EventReportResult);
                BindEventLogGrid(bundle.EventReportResult);

                ShowReportPagesAfterGenerate();
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در تولید گزارش:", ex, "خطا");
            }
            finally
            {
                if (formUpdateStarted)
                    EndFormUpdate();

                btnGenerateReport.Text = "Generate Report";
                UpdateReportActionButtonsState();
            }
        }

        /// <summary>
        /// نتایج آماری tbl_data را در گرید Summary نمایش می‌دهد.
        /// </summary>
        private void BindSummaryGrid(ReportResult result)
        {
            RunGridUpdate(dgvSummary, () =>
            {
                foreach (DataGridViewRow row in dgvSummary.Rows)
                {
                    row.Cells["colMin"].Value = "";
                    row.Cells["colMax"].Value = "";
                    row.Cells["colAvg"].Value = "";
                }

                var dataParameters = _reportProfile.Parameters
                    .Where(p => p.DataColumnName != null)
                    .Where(p => p.SupportedAggregations.Contains(ReportAggregationType.Min))
                    .Where(p => p.SupportedAggregations.Contains(ReportAggregationType.Max))
                    .Where(p => p.SupportedAggregations.Contains(ReportAggregationType.Avg))
                    .Where(p => p.Category != ReportParameterCategory.RPM)
                    .ToList();

                for (int i = 0; i < dataParameters.Count && i < dgvSummary.Rows.Count; i++)
                {
                    ReportParameterDefinition parameter = dataParameters[i];

                    List<ReportSummaryItem> items = result.SummaryItems
                        .Where(x => x.ParameterKey == parameter.Key)
                        .ToList();

                    dgvSummary.Rows[i].Cells["colMin"].Value =
                        FormatSummaryValue(items, ReportAggregationType.Min);

                    dgvSummary.Rows[i].Cells["colMax"].Value =
                        FormatSummaryValue(items, ReportAggregationType.Max);

                    dgvSummary.Rows[i].Cells["colAvg"].Value =
                        FormatSummaryValue(items, ReportAggregationType.Avg);
                }
            });

            DisableGridSelectionVisual(dgvSummary);
        }
        /// <summary>
        /// نتایج تجمیعی tbl_unique را در گرید Unique Summary نمایش می‌دهد.
        /// </summary>
        private void BindUniqueSummaryGrid(ReportResult result)
        {
            RunGridUpdate(dgvUniqueSummary, () =>
            {
                double gasGeneratorFuel = GetSummaryValue(result, "ir_f", ReportAggregationType.Sum);
                double turbineFuel = GetSummaryValue(result, "turbine_fuel", ReportAggregationType.Sum);
                double turbineFlow = GetSummaryValue(result, "turbine_flow", ReportAggregationType.Sum);
                double nonTurbineFlow = GetSummaryValue(result, "non_turbine_flow", ReportAggregationType.Sum);
                double vent = GetSummaryValue(result, "vent", ReportAggregationType.Sum);

                dgvUniqueSummary.Rows[0].Cells["colValue"].Value = gasGeneratorFuel.ToString("F1");
                dgvUniqueSummary.Rows[1].Cells["colValue"].Value = turbineFuel.ToString("F1");
                dgvUniqueSummary.Rows[2].Cells["colValue"].Value = (gasGeneratorFuel + turbineFuel).ToString("F1");

                dgvUniqueSummary.Rows[3].Cells["colValue"].Value = turbineFlow.ToString("F1");
                dgvUniqueSummary.Rows[4].Cells["colValue"].Value = nonTurbineFlow.ToString("F1");
                dgvUniqueSummary.Rows[5].Cells["colValue"].Value = (turbineFlow + nonTurbineFlow).ToString("F1");

                dgvUniqueSummary.Rows[6].Cells["colValue"].Value = vent.ToString("F1");
                dgvUniqueSummary.Rows[7].Cells["colValue"].Value = _currentRecycleChangeCount.ToString();
            });

            DisableGridSelectionVisual(dgvUniqueSummary);
        }

        /// <summary>
        /// داده‌های گزارش رویدادها را در گرید نمایش می‌دهد.
        /// </summary>
        private void BindEventSummaryGrid(EventReportResult result)
        {
            RunGridUpdate(dgvEventSummary, () =>
            {
                foreach (DataGridViewRow row in dgvEventSummary.Rows)
                {
                    for (int i = 1; i < dgvEventSummary.Columns.Count; i++)
                        row.Cells[i].Value = "";
                }

                Dictionary<string, UnitEventSummary> map =
                    result.UnitSummaries.ToDictionary(x => x.Unit);

                foreach (string unit in _reportProfile.Units)
                {
                    if (!map.TryGetValue(unit, out UnitEventSummary? summary))
                        continue;

                    dgvEventSummary.Rows[0].Cells[$"col{unit}"].Value = summary.RuntimeHours;
                    dgvEventSummary.Rows[1].Cells[$"col{unit}"].Value = summary.RuntimeAfterOH;
                    dgvEventSummary.Rows[2].Cells[$"col{unit}"].Value = summary.TotalEvents;
                    dgvEventSummary.Rows[3].Cells[$"col{unit}"].Value = summary.StartCount;
                    dgvEventSummary.Rows[4].Cells[$"col{unit}"].Value = summary.NSDCount;
                    dgvEventSummary.Rows[5].Cells[$"col{unit}"].Value = summary.ESDCount;
                    dgvEventSummary.Rows[6].Cells[$"col{unit}"].Value = summary.EsdExtraHoursTotal;
                    dgvEventSummary.Rows[7].Cells[$"col{unit}"].Value = summary.LongestRunHours;

                    dgvEventSummary.Rows[8].Cells[$"col{unit}"].Value = summary.DayStartCount;
                    dgvEventSummary.Rows[9].Cells[$"col{unit}"].Value = summary.NightStartCount;
                    dgvEventSummary.Rows[10].Cells[$"col{unit}"].Value = summary.DayNSDCount;
                    dgvEventSummary.Rows[11].Cells[$"col{unit}"].Value = summary.NightNSDCount;
                    dgvEventSummary.Rows[12].Cells[$"col{unit}"].Value = summary.DayESDCount;
                    dgvEventSummary.Rows[13].Cells[$"col{unit}"].Value = summary.NightESDCount;
                }

                CalculateEventSummaryTotals();
            });

            DisableGridSelectionVisual(dgvEventSummary);
        }

        /// <summary>
        /// محاسبه مقدار Total برای هر سطر.
        /// </summary>
        private void CalculateEventSummaryTotals()
        {
            int totalColumnIndex = dgvEventSummary.Columns["colTotal"].Index;

            foreach (DataGridViewRow row in dgvEventSummary.Rows)
            {
                double sum = 0;
                double max = double.MinValue;

                for (int i = 1; i < totalColumnIndex; i++)
                {
                    object? value = row.Cells[i].Value;

                    if (value == null)
                        continue;

                    if (double.TryParse(value.ToString(), out double num))
                    {
                        sum += num;

                        if (num > max)
                            max = num;
                    }
                }

                // سطر Max Runtime (index 7)
                if (row.Index == 7)
                {
                    row.Cells[totalColumnIndex].Value = "";

                }
                else
                {
                    row.Cells[totalColumnIndex].Value =
                        Math.Abs(sum) < 0.0001 ? "" : sum.ToString("F1");
                }
            }
        }

        /// <summary>
        /// روزهای سرویس هر واحد و تعداد روزهای همزمانی واحدها را در یک گرید چهار ستونه نمایش می‌دهد.
        /// ستون‌های اول و دوم مربوط به Service Days واحدها هستند.
        /// ستون‌های سوم و چهارم مربوط به تعداد روزهای Single / Two Units / Three Units و ... هستند.
        /// </summary>
        private void BindServiceDaysGrid(EventReportResult result)
        {
            RunGridUpdate(dgvServiceDays, () =>
            {
                dgvServiceDays.Rows.Clear();

                List<(string Title, int Days)> unitRows = [];

                foreach (string unit in _reportProfile.Units)
                {
                    int serviceDaysCount = result.ServiceDaysByUnit.TryGetValue(unit, out HashSet<long>? days)
                        ? days.Count
                        : 0;

                    unitRows.Add((GetUnitDisplayName(unit), serviceDaysCount));
                }

                Dictionary<long, int> activeUnitsPerDay = [];

                foreach (HashSet<long> days in result.ServiceDaysByUnit.Values)
                {
                    foreach (long day in days)
                    {
                        activeUnitsPerDay[day] = activeUnitsPerDay.TryGetValue(day, out int count)
                            ? count + 1
                            : 1;
                    }
                }

                List<(string Title, int Days)> combinationRows = [];

                for (int count = 1; count <= _reportProfile.Units.Count; count++)
                {
                    int daysCount = activeUnitsPerDay.Values.Count(x => x == count);

                    combinationRows.Add((GetCombinationDisplayName(count), daysCount));
                }

                int maxRows = Math.Max(unitRows.Count, combinationRows.Count);

                for (int i = 0; i < maxRows; i++)
                {
                    string unitTitle = i < unitRows.Count ? unitRows[i].Title : "";
                    object unitDays = i < unitRows.Count ? unitRows[i].Days : "";

                    string combinationTitle = i < combinationRows.Count ? combinationRows[i].Title : "";
                    object combinationDays = i < combinationRows.Count ? combinationRows[i].Days : "";

                    dgvServiceDays.Rows.Add(
                        unitTitle,
                        unitDays,
                        combinationTitle,
                        combinationDays);
                }


            });

            DisableGridSelectionVisual(dgvServiceDays);
        }


        /// <summary>
        /// روزهای سرویس هر واحد و تعداد روزهای همزمانی واحدها را
        /// بر اساس داده‌های نهایی ذخیره‌شده نمایش می‌دهد.
        /// این متد برای ماه‌های قفل‌شده استفاده می‌شود.
        /// </summary>
        private void BindServiceDaysGridFromSnapshot(Dictionary<string, double> serviceSummaryMap)
        {
            RunGridUpdate(dgvServiceDays, () =>
            {
                dgvServiceDays.Rows.Clear();

                List<(string Title, int Days)> unitRows = [];

                foreach (string unit in _reportProfile.Units)
                {
                    string key = $"unit_service_days_{unit}";

                    int days = serviceSummaryMap.TryGetValue(key, out double value)
                        ? Convert.ToInt32(value)
                        : 0;

                    unitRows.Add((GetUnitDisplayName(unit), days));
                }

                List<(string Title, int Days)> combinationRows = [];

                for (int count = 1; count <= _reportProfile.Units.Count; count++)
                {
                    string key = $"combination_{count}_units_days";

                    int days = serviceSummaryMap.TryGetValue(key, out double value)
                        ? Convert.ToInt32(value)
                        : 0;

                    combinationRows.Add((GetCombinationDisplayName(count), days));
                }

                int maxRows = Math.Max(unitRows.Count, combinationRows.Count);

                for (int i = 0; i < maxRows; i++)
                {
                    string unitTitle = i < unitRows.Count ? unitRows[i].Title : "";
                    object unitDays = i < unitRows.Count ? unitRows[i].Days : "";

                    string combinationTitle = i < combinationRows.Count ? combinationRows[i].Title : "";
                    object combinationDays = i < combinationRows.Count ? combinationRows[i].Days : "";

                    dgvServiceDays.Rows.Add(
                        unitTitle,
                        unitDays,
                        combinationTitle,
                        combinationDays);
                }
            });

            DisableGridSelectionVisual(dgvServiceDays);
        }


        /// <summary>
        /// لاگ رویدادها را بر اساس حالت انتخاب‌شده در گرید نمایش می‌دهد.
        /// </summary>
        private void BindEventLogGrid(EventReportResult result)
        {
            RunGridUpdate(dgvEventLog, () =>
            {
                dgvEventLog.Rows.Clear();

                dgvEventLog.Columns["colItem"].HeaderText =
                    rdoLogByUnit.Checked ? "Event" : "Unit";

                List<EventLogItem> sortedItems = result.EventLogItems
                    .OrderBy(x => x.EventDateTime)
                    .ThenBy(x => x.Unit)
                    .ToList();

                if (sortedItems.Count == 0)
                    return;

                if (rdoLogByUnit.Checked)
                    BindEventLogByUnit(sortedItems);
                else
                    BindEventLogByEvent(sortedItems);
            });

            DisableGridSelectionVisual(dgvEventLog);
        }

        /// <summary>
        /// لاگ رویدادها را بر اساس واحدها گروه‌بندی و نمایش می‌دهد.
        /// </summary>
        private void BindEventLogByUnit(List<EventLogItem> items)
        {
            var groupedItems = items
                .GroupBy(x => x.Unit)
                .OrderBy(x => x.Key);

            foreach (var group in groupedItems)
            {
                AddEventLogHeaderRow(GetUnitDisplayName(group.Key));

                foreach (EventLogItem item in group)
                {
                    dgvEventLog.Rows.Add(
                        "",
                        FormatPersianDate(item.EventDate),
                        item.EventType,
                        item.EventTime,
                        item.Remark);
                }
            }
        }

        /// <summary>
        /// لاگ رویدادها را بر اساس نوع رویداد گروه‌بندی و نمایش می‌دهد.
        /// </summary>
        private void BindEventLogByEvent(List<EventLogItem> items)
        {
            var groupedItems = items
                .GroupBy(x => x.EventType)
                .OrderBy(x => x.Key);

            foreach (var group in groupedItems)
            {
                AddEventLogHeaderRow(group.Key);

                foreach (EventLogItem item in group)
                {
                    dgvEventLog.Rows.Add(
                        "",
                        FormatPersianDate(item.EventDate),
                        GetUnitDisplayName(item.Unit),
                        item.EventTime,
                        item.Remark);
                }
            }
        }


        /// <summary>
        /// ترکیب روزهای سرویس را فقط بر اساس خروجی رویدادها نمایش می‌دهد
        /// برای هر روز بازه مشخص می‌کند کدام واحدها طبق tbl_events در سرویس بوده‌اند
        /// اگر در یک روز هیچ واحدی در سرویس نباشد، مقدار No Unit ثبت می‌شود
        /// </summary>
        private void BindServiceCombinationGrid(EventReportResult result)
        {
            RunGridUpdate(dgvServiceCombination, () =>
            {
                dgvServiceCombination.Rows.Clear();

                Dictionary<long, List<string>> activeUnitsByDay =
                    BuildActiveUnitsByDayFromEvents(result);

                Dictionary<string, int> combinationFrequency = [];

                Dictionary<int, List<(long DateRep, string UnitText)>> groupedRows = [];

                foreach (long day in GetPersianDateRange(_currentReportDateFrom, _currentReportDateTo))
                {
                    List<string> units = activeUnitsByDay.TryGetValue(day, out List<string>? activeUnits)
                        ? activeUnits
                        : [];

                    string unitText = FormatUnitCombination(units);

                    if (string.IsNullOrWhiteSpace(unitText))
                        unitText = "No Unit";

                    int activeCount = unitText == "No Unit"
                        ? 0
                        : units.Distinct(StringComparer.OrdinalIgnoreCase).Count();

                    if (!groupedRows.TryGetValue(activeCount, out List<(long DateRep, string UnitText)>? rows))
                    {
                        rows = [];
                        groupedRows[activeCount] = rows;
                    }

                    rows.Add((day, unitText));

                    if (!combinationFrequency.TryAdd(unitText, 1))
                        combinationFrequency[unitText]++;
                }

                foreach (KeyValuePair<int, List<(long DateRep, string UnitText)>> group in groupedRows.OrderBy(x => x.Key))
                {
                    string title = group.Key == 0
                        ? "No Unit In Service"
                        : GetCombinationDisplayName(group.Key);

                    int headerRowIndex = dgvServiceCombination.Rows.Add($"{title}", "", "");

                    DataGridViewRow headerRow = dgvServiceCombination.Rows[headerRowIndex];
                    headerRow.DefaultCellStyle.BackColor = AppThemeManager.CurrentPalette.GridFixedCellBackColor;
                    headerRow.DefaultCellStyle.ForeColor = AppThemeManager.CurrentPalette.TextPrimaryColor;
                    headerRow.DefaultCellStyle.Font = new Font("Tahoma", 8F, FontStyle.Bold);

                    foreach ((long DateRep, string UnitText) item in group.Value.OrderBy(x => x.DateRep))
                    {
                        dgvServiceCombination.Rows.Add(
                            "",
                            FormatPersianDate(item.DateRep),
                            item.UnitText);
                    }
                }

                string mostFrequentText = BuildMostFrequentCombinationText(combinationFrequency);

                // TODO:
                // محل نهایی نمایش این خلاصه بعداً در UI مشخص می‌شود
                // lblMostFrequentCombination.Text = mostFrequentText;
            });

            DisableGridSelectionVisual(dgvServiceCombination);
        }


        /// <summary>
        /// تاریخ‌های وقوع حداقل و حداکثر پارامترها را به صورت لاگ‌محور نمایش می‌دهد.
        /// اگر یک مقدار Min یا Max در چندین روز رخ داده باشد، همه تاریخ‌ها نمایش داده می‌شوند.
        /// </summary>
        private void BindExtremeDatesGrid(List<ExtremeDateItem> items)
        {
            RunGridUpdate(dgvExtremeDates, () =>
            {
                dgvExtremeDates.Rows.Clear();

                foreach (ExtremeDateItem item in items)
                {
                    string shortName = GetShortParameterName(item.ParameterKey, item.DisplayName);

                    AddExtremeDatesHeaderRow(shortName);

                    foreach (long date in item.MinDates)
                    {
                        int rowIndex = dgvExtremeDates.Rows.Add();

                        dgvExtremeDates.Rows[rowIndex].Cells["colGroup"].Value = "";
                        dgvExtremeDates.Rows[rowIndex].Cells["colDate"].Value = FormatPersianDate(date);
                        dgvExtremeDates.Rows[rowIndex].Cells["colType"].Value = "Min";
                        dgvExtremeDates.Rows[rowIndex].Cells["colValue"].Value =
                            item.MinValue.HasValue ? item.MinValue.Value.ToString("F1") : "";
                    }

                    foreach (long date in item.MaxDates)
                    {
                        int rowIndex = dgvExtremeDates.Rows.Add();

                        dgvExtremeDates.Rows[rowIndex].Cells["colGroup"].Value = "";
                        dgvExtremeDates.Rows[rowIndex].Cells["colDate"].Value = FormatPersianDate(date);
                        dgvExtremeDates.Rows[rowIndex].Cells["colType"].Value = "Max";
                        dgvExtremeDates.Rows[rowIndex].Cells["colValue"].Value =
                            item.MaxValue.HasValue ? item.MaxValue.Value.ToString("F1") : "";
                    }
                }
            });

            DisableGridSelectionVisual(dgvExtremeDates);
        }

        /// <summary>
        /// تعداد دفعات تغییر مقدار پارامتر rec بین صفر و غیرصفر را محاسبه می‌کند.
        /// </summary>
        private int CalculateRecycleChanges(SqliteConnection conn, long dateFrom, long dateTo)
        {
            using SqliteCommand cmd = conn.CreateCommand();

            cmd.CommandText =
                """
        SELECT rec
        FROM tbl_data
        WHERE date_rep BETWEEN $from AND $to
        ORDER BY date_rep, time_rep;
        """;

            cmd.Parameters.AddWithValue("$from", dateFrom);
            cmd.Parameters.AddWithValue("$to", dateTo);

            using SqliteDataReader reader = cmd.ExecuteReader();

            bool? lastWasZero = null;
            int changeCount = 0;

            while (reader.Read())
            {
                if (reader["rec"] == DBNull.Value)
                    continue;

                double value = Convert.ToDouble(reader["rec"]);

                bool isZero = Math.Abs(value) < 0.000001;

                if (lastWasZero.HasValue && lastWasZero.Value != isZero)
                    changeCount++;

                lastWasZero = isZero;
            }

            return changeCount;
        }


        //==========================================================================================================
        #endregion

        #region Helper Methods

        //6-Helper Methods==================================================================================

        /// <summary>
        /// عدد ماه انتخاب‌شده را از ComboBox ماه استخراج می‌کند.
        /// این متد برای ماه‌های فارسی استفاده می‌شود و وابسته به Convert.ToInt32 نیست.
        /// </summary>
        private bool TryGetSelectedMonthNumber(out int month)
        {
            month = 0;

            if (cmbMonth.SelectedIndex < 0)
                return false;

            month = cmbMonth.SelectedIndex + 1;

            return month >= 1 && month <= 12;
        }

        /// <summary>
        /// ساخت درخواست گزارش ماهانه برای نهایی‌سازی.
        /// بازه کامل ماه را پوشش می‌دهد و پارامترهای انتخاب‌شده کاربر را اعمال می‌کند.
        /// </summary>
        private ReportRequest BuildMonthlyReportRequestForFinalize(int year, int month)
        {
            int dateFrom = year * 10000 + month * 100 + 1;

            int lastDay = PersianDateHelper.GetDaysInMonth(year, month);

            int dateTo = year * 10000 + month * 100 + lastDay;

            var request = new ReportRequest
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                Granularity = ReportGranularity.Monthly,
                IncludeMissingDays = true
            };

            // افزودن پارامترهای انتخاب‌شده (نه Set کردن)
            request.SelectedParameters.AddRange(GetAllReportParameterKeysForFinalize());

            return request;
        }

        /// <summary>
        /// دریافت کلید تمام پارامترهای قابل گزارش برای ایستگاه فعال.
        /// در نهایی‌سازی ماهانه نباید وابسته به انتخاب موقت کاربر باشیم،
        //— بلکه Snapshot باید کامل ساخته شود.
        /// </summary>
        private List<string> GetAllReportParameterKeysForFinalize()
        {
            ReportStationProfile profile =
                ReportStationProfileProvider.GetProfile(_stationName);

            return profile.Parameters
                .Select(p => p.Key)
                .ToList();
        }

        /// <summary>
        /// از خروجی موتور رویدادها، برای هر تاریخ لیست واحدهای در سرویس را می‌سازد
        /// منبع این محاسبه فقط tbl_events است، چون ServiceDaysByUnit از EventReportEngineService تولید می‌شود
        /// </summary>
        private static Dictionary<long, List<string>> BuildActiveUnitsByDayFromEvents(EventReportResult result)
        {
            Dictionary<long, List<string>> activeUnitsByDay = [];

            foreach (KeyValuePair<string, HashSet<long>> pair in result.ServiceDaysByUnit)
            {
                string unit = NormalizeUnitKey(pair.Key);

                foreach (long day in pair.Value)
                {
                    if (!activeUnitsByDay.TryGetValue(day, out List<string>? units))
                    {
                        units = [];
                        activeUnitsByDay[day] = units;
                    }

                    if (!units.Contains(unit, StringComparer.OrdinalIgnoreCase))
                        units.Add(unit);
                }
            }

            return activeUnitsByDay;
        }

        /// <summary>
        /// تمام تاریخ‌های شمسی بین دو تاریخ را به صورت پیوسته تولید می‌کند
        /// </summary>
        private static IEnumerable<long> GetPersianDateRange(long dateFrom, long dateTo)
        {
            DateTime current = ConvertPersianLongToGregorian(dateFrom);
            DateTime end = ConvertPersianLongToGregorian(dateTo);

            while (current <= end)
            {
                yield return ConvertGregorianToPersianLong(current);
                current = current.AddDays(1);
            }
        }

        /// <summary>
        /// تاریخ شمسی عددی را به تاریخ میلادی تبدیل می‌کند
        /// </summary>
        private static DateTime ConvertPersianLongToGregorian(long persianDate)
        {
            int year = (int)(persianDate / 10000);
            int month = (int)((persianDate / 100) % 100);
            int day = (int)(persianDate % 100);

            System.Globalization.PersianCalendar calendar = new();

            return calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        }

        /// <summary>
        /// تاریخ میلادی را به تاریخ شمسی عددی تبدیل می‌کند
        /// </summary>
        private static long ConvertGregorianToPersianLong(DateTime date)
        {
            System.Globalization.PersianCalendar calendar = new();

            int year = calendar.GetYear(date);
            int month = calendar.GetMonth(date);
            int day = calendar.GetDayOfMonth(date);

            return year * 10000L + month * 100L + day;
        }

        /// <summary>
        /// لیست واحدها را به فرمت استاندارد انگلیسی مثل U1 + U3 تبدیل می‌کند
        /// </summary>
        private static string FormatUnitCombination(IEnumerable<string> units)
        {
            return string.Join(" + ",
                units
                    .Select(NormalizeUnitKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(GetUnitSortNumber)
                    .ThenBy(x => x));
        }

        /// <summary>
        /// نام واحد را به فرم استاندارد U1 تبدیل می‌کند
        /// </summary>
        private static string NormalizeUnitKey(string? unit)
        {
            return (unit ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace("UNIT", "U")
                .Replace(" ", "");
        }

        /// <summary>
        /// شماره واحد را برای مرتب‌سازی طبیعی استخراج می‌کند
        /// </summary>
        private static int GetUnitSortNumber(string unit)
        {
            string normalized = NormalizeUnitKey(unit);

            if (normalized.Length > 1 &&
                normalized[0] == 'U' &&
                int.TryParse(normalized[1..], out int number))
            {
                return number;
            }

            return int.MaxValue;
        }

        /// <summary>
        /// پرتکرارترین ترکیب واحدهای در سرویس را محاسبه و متن انگلیسی آن را تولید می‌کند
        /// </summary>
        private static string BuildMostFrequentCombinationText(Dictionary<string, int> combinationFrequency)
        {
            if (combinationFrequency.Count == 0)
                return "Most Frequent Combination: No data";

            int maxCount = combinationFrequency.Values.Max();

            List<string> topCombinations = combinationFrequency
                .Where(x => x.Value == maxCount)
                .OrderBy(x => x.Key)
                .Select(x => x.Key)
                .ToList();

            string combinationsText = string.Join(" | ", topCombinations);

            return $"Most Frequent Combination: {combinationsText} ({maxCount} day{(maxCount > 1 ? "s" : "")})";
        }


        /// <summary>
        /// از خروجی ServiceDaysByUnit، برای هر تاریخ لیست واحدهای در سرویس را می‌سازد.
        /// این ساختار پایه هم برای نمایش روزانه و هم برای تحلیل ترکیب‌های پرتکرار استفاده می‌شود.
        /// </summary>
        private static Dictionary<long, List<string>> BuildActiveUnitsByDay(EventReportResult result)
        {
            Dictionary<long, List<string>> activeUnitsByDay = [];

            foreach (KeyValuePair<string, HashSet<long>> pair in result.ServiceDaysByUnit)
            {
                string unit = NormalizeUnitKey(pair.Key);

                foreach (long day in pair.Value)
                {
                    if (!activeUnitsByDay.TryGetValue(day, out List<string>? units))
                    {
                        units = [];
                        activeUnitsByDay[day] = units;
                    }

                    if (!units.Contains(unit))
                        units.Add(unit);
                }
            }

            return activeUnitsByDay;
        }


        private static string GetShortParameterName(string key, string displayName)
        {
            return key switch
            {
                "in_p" => "InletPress",
                "out_p" => "OutletPress",
                "flow" => "Flow",
                "out_t" => "Out.Temp",
                "amb_t" => "Amb.Temp",
                _ => displayName
            };
        }

        private static void BeginGridUpdate(DataGridView dgv)
        {
            SendMessage(dgv.Handle, WM_SETREDRAW, false, IntPtr.Zero);
        }

        private static void EndGridUpdate(DataGridView dgv)
        {
            SendMessage(dgv.Handle, WM_SETREDRAW, true, IntPtr.Zero);
            dgv.Refresh();
        }

        private void BeginFormUpdate()
        {
            SendMessage(Handle, WM_SETREDRAW, false, IntPtr.Zero);
        }

        private void EndFormUpdate()
        {
            SendMessage(Handle, WM_SETREDRAW, true, IntPtr.Zero);
            Refresh();
        }

        /// <summary>
        /// خروجی موقت تولید گزارش async را نگهداری می‌کند.
        /// </summary>
        private sealed class ReportGenerationBundle
        {
            public ReportResult ReportResult { get; init; } = new();

            public EventReportResult? EventReportResult { get; init; }

            public List<ExtremeDateItem> ExtremeDateItems { get; init; } = [];

            public int RecycleChangeCount { get; init; }

            public bool HasIncompleteDays { get; init; }

            public bool WasOverridden { get; init; }
        }

        /// <summary>
        /// اجرای عملیات روی گرید با توقف موقت Layout برای کاهش Flicker.
        /// </summary>
        private static void RunGridUpdate(DataGridView dgv, Action action)
        {
            dgv.SuspendLayout();
            BeginGridUpdate(dgv);

            try
            {
                action();
            }
            finally
            {
                EndGridUpdate(dgv);
                dgv.ResumeLayout();

                dgv.ClearSelection();
                dgv.CurrentCell = null;
            }
        }

        /// <summary>
        /// حذف اثر بصری انتخاب سلول در DataGridView
        /// </summary>
        private static void DisableGridSelectionVisual(DataGridView dgv)
        {
            dgv.ClearSelection();
            dgv.CurrentCell = null;

            dgv.DefaultCellStyle.SelectionBackColor = dgv.DefaultCellStyle.BackColor;
            dgv.DefaultCellStyle.SelectionForeColor = dgv.DefaultCellStyle.ForeColor;

            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor =
                dgv.ColumnHeadersDefaultCellStyle.BackColor;

            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor =
                dgv.ColumnHeadersDefaultCellStyle.ForeColor;

            foreach (DataGridViewColumn col in dgv.Columns)
            {
                col.DefaultCellStyle.SelectionBackColor = col.DefaultCellStyle.BackColor;
                col.DefaultCellStyle.SelectionForeColor = col.DefaultCellStyle.ForeColor;
            }
        }

        /// <summary>
        /// مقدار خلاصه آماری یک پارامتر را با فرمت یک رقم اعشار برمی‌گرداند.
        /// </summary>
        private static string FormatSummaryValue(List<ReportSummaryItem> items, ReportAggregationType aggregationType)
        {
            double? value = items
                .FirstOrDefault(x => x.AggregationType == aggregationType)
                ?.Value;

            return value.HasValue ? value.Value.ToString("F1") : "";
        }

        /// <summary>
        /// مقدار عددی خلاصه آماری یک پارامتر را برمی‌گرداند.
        /// اگر مقدار وجود نداشته باشد، صفر برمی‌گرداند.
        /// </summary>
        private static double GetSummaryValue(ReportResult result, string parameterKey, ReportAggregationType aggregationType)
        {
            return result.SummaryItems
                .FirstOrDefault(x =>
                    x.ParameterKey == parameterKey &&
                    x.AggregationType == aggregationType)
                ?.Value ?? 0;
        }

        /// <summary>
        /// تعداد روزهای ماه شمسی را بر اساس سال و ماه برمی‌گرداند.
        /// </summary>
        private static int GetPersianMonthDays(int year, int month)
        {
            System.Globalization.PersianCalendar calendar = new();

            return calendar.GetDaysInMonth(year, month);
        }

        /// <summary>
        /// تبدیل نام داخلی یونیت (U1) به نام نمایشی (Unit 1)
        /// </summary>
        private static string GetUnitDisplayName(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
                return "";

            // حالت استاندارد: U1, U2, ...
            if (unit.StartsWith("U", StringComparison.OrdinalIgnoreCase))
                return unit.ToUpper();

            // اگر از دیتابیس به صورت "1" بیاد
            if (int.TryParse(unit, out int num))
                return $"U{num}";

            // اگر به صورت "Unit 1" بیاد
            if (unit.StartsWith("Unit", StringComparison.OrdinalIgnoreCase))
            {
                string numberPart = unit.Replace("Unit", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (int.TryParse(numberPart, out int n))
                    return $"U{n}";
            }

            return unit;
        }

        /// <summary>
        /// تبدیل تعداد یونیت‌های فعال به عنوان نمایشی.
        /// </summary>
        private string GetCombinationDisplayName(int count)
        {
            return count switch
            {
                1 => "Single",
                2 => "TwoUnits",
                3 => "ThreeUnits",
                4 => "FourUnits",
                _ => $"{count}Units"
            };
        }

        /// <summary>
        /// یک ردیف هدر برای گروه‌بندی لاگ رویدادها اضافه می‌کند.
        /// </summary>
        private void AddEventLogHeaderRow(string title)
        {
            int rowIndex = dgvEventLog.Rows.Add($"{title}", "", "", "", "");

            DataGridViewRow row = dgvEventLog.Rows[rowIndex];

            row.DefaultCellStyle.BackColor = AppThemeManager.CurrentPalette.GridFixedCellBackColor;
            row.DefaultCellStyle.ForeColor = AppThemeManager.CurrentPalette.TextPrimaryColor;
            row.DefaultCellStyle.Font = new Font("tahoma", 8.5F, FontStyle.Bold);
        }

        /// <summary>
        /// تاریخ عددی شمسی را به قالب نمایشی yyyy/MM/dd تبدیل می‌کند.
        /// </summary>
        private static string FormatPersianDate(long dateRep)
        {
            int year = (int)(dateRep / 10000);
            int month = (int)((dateRep / 100) % 100);
            int day = (int)(dateRep % 100);

            return $"{year:0000}/{month:00}/{day:00}";
        }

        /// <summary>
        /// هشدارهای مربوط به ناقص بودن داده‌ها را نمایش می‌دهد.
        /// </summary>
        private void ShowReportWarnings(ReportResult result)
        {
            if (result?.Warnings == null || result.Warnings.Count == 0)
                return;

            string message = string.Join(Environment.NewLine, result.Warnings);

            UiMessageService.ShowWarning("هشدار در داده‌های گزارش:" + Environment.NewLine + Environment.NewLine + message, "اعتبارسنجی");
        }

        /// <summary>
        /// لیست تاریخ‌های عددی شمسی را برای نمایش در گرید فرمت می‌کند.
        /// </summary>
        private static string FormatDateList(List<long> dates)
        {
            if (dates.Count == 0)
                return "";

            return string.Join(", ", dates.Select(FormatPersianDate));
        }


        /// <summary>
        /// یک ردیف هدر برای گروه‌بندی پارامترها در گرید Extreme Dates اضافه می‌کند.
        /// </summary>
        private void AddExtremeDatesHeaderRow(string title)
        {
            int rowIndex = dgvExtremeDates.Rows.Add($"{title}", "", "", "", "");

            DataGridViewRow row = dgvExtremeDates.Rows[rowIndex];

            row.DefaultCellStyle.BackColor = AppThemeManager.CurrentPalette.GridFixedCellBackColor;
            row.DefaultCellStyle.ForeColor = AppThemeManager.CurrentPalette.TextPrimaryColor;
            row.DefaultCellStyle.Font = new Font("tahoma", 8F, FontStyle.Bold);
        }

        private static bool HasIncompleteDays(ReportResult report)
        {
            return report.DailyStatuses.Any(x => !x.IsComplete);
        }

        private bool IsGeneratedReportCurrent(ReportRequest request)
        {
            return _currentGeneratedRequest != null &&
                   _currentGeneratedReportResult != null &&
                   _currentEventReportResult != null &&
                   _currentGeneratedRequest.DateFrom == request.DateFrom &&
                   _currentGeneratedRequest.DateTo == request.DateTo &&
                   _currentGeneratedRequest.Granularity == request.Granularity;
        }

        private void ClearGeneratedReportCache()
        {
            _currentGeneratedRequest = null;
            _currentGeneratedReportResult = null;
            _currentEventReportResult = null;
            _currentRecycleChangeCount = 0;
        }

        private void UpdateReportActionButtonsState()
        {
            bool hasValidYear = cmbYear.SelectedItem != null;
            bool hasValidMonth = TryGetSelectedMonthNumber(out int month);

            btnGenerateReport.Enabled =
                hasValidYear &&
                (!rdoMonthly.Checked || hasValidMonth);

            btnFinalizeMonthlyReport.Enabled = false;
            btnPDF.Enabled = false;

            if (!rdoMonthly.Checked || !hasValidYear || !hasValidMonth)
                return;

            int year = Convert.ToInt32(cmbYear.SelectedItem);

            bool isLocked = MonthlyLockService.IsMonthLocked(year, month);

            btnFinalizeMonthlyReport.Enabled = !isLocked;
            btnPDF.Enabled = isLocked;
        }


        //==========================================================================================================
        #endregion

        #region Create PDF

        /// <summary>
        /// مسیر ذخیره فایل PDF را از کاربر دریافت کرده و گزارش رسمی ماهانه را تولید می‌کند.
        /// PDF فقط از داده‌های نهایی ذخیره‌شده ساخته می‌شود.
        /// </summary>
        private void ExportMonthlyFinalPdf(int year, int month)
        {
            if (!MonthlyLockService.IsMonthLocked(year, month))
            {
                UiMessageService.ShowWarning("ابتدا باید گزارش نهایی این ماه ایجاد شود", "اعتبارسنجی");
                return;
            }

            using SaveFileDialog dialog = new()
            {
                Title = "Save Monthly Final Report",
                Filter = "PDF File (*.pdf)|*.pdf",
                FileName = $"Monthly_Final_Report_{_reportProfile.StationName}_{year}_{month:00}.pdf",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                MonthlyFinalPdfService.GenerateMonthlyFinalPdf(
                    year,
                    month,
                    dialog.FileName,
                    _reportProfile.StationName);

                UiMessageService.ShowInfo("گزارش نهایی با موفقیت ایجاد شد", "اطلاع");

            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در ساخت فایل :", ex, "خطا");
            }
        }

        #endregion

        #region Buttons
        //7-Buttons==================================================================================


        /// <summary>
        /// نهایی‌سازی گزارش ماهانه.
        /// این عملیات Snapshot نهایی را ذخیره کرده و ماه را قفل می‌کند.
        /// صدور PDF از دکمه جداگانه انجام می‌شود.
        /// </summary>

        private void btnFinalizeMonthlyReport_Click(object sender, EventArgs e)
        {
            try
            {
                if (!rdoMonthly.Checked)
                {
                    UiMessageService.ShowWarning("نهایی‌سازی فقط برای گزارش ماهانه قابل انجام است", "اعتبارسنجی");
                    return;
                }

                if (cmbYear.SelectedItem == null)
                {
                    UiMessageService.ShowWarning("سال گزارش را انتخاب کنید", "اعتبارسنجی");
                    return;
                }

                if (!TryGetSelectedMonthNumber(out int month))
                {
                    UiMessageService.ShowWarning("ماه گزارش را انتخاب کنید", "اعتبارسنجی");
                    return;
                }

                int year = Convert.ToInt32(cmbYear.SelectedItem);

                if (MonthlyLockService.IsMonthLocked(year, month))
                {
                    UiMessageService.ShowInfo("این ماه قبلاً نهایی و قفل شده است", "اطلاع");
                    return;
                }

                if (!TryBuildReportRequest(out ReportRequest request))
                    return;

                if (!IsGeneratedReportCurrent(request))
                {
                    UiMessageService.ShowWarning("ابتدا گزارش همین ماه را تولید کنید", "اعتبارسنجی");
                    return;
                }

                if (_currentGeneratedReportResult == null || _currentEventReportResult == null)
                {
                    MessageBox.Show(
                        "اطلاعات گزارش برای نهایی‌سازی آماده نیست. گزارش را دوباره تولید کنید",
                        "Finalize Month",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (HasIncompleteDays(_currentGeneratedReportResult))
                {
                    MessageBox.Show(
                        "داده‌های این ماه کامل نیست" +
                        Environment.NewLine +
                        Environment.NewLine +
                        "تا زمانی که تمام روزهای ماه کامل ثبت نشوند، گزارش نهایی ایجاد نمی‌شود",
                        "داده ناقص",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                DialogResult confirm = MessageBox.Show(
                    $"آیا می‌خواهید گزارش ماه {year}/{month:00} نهایی شود؟" +
                    Environment.NewLine +
                    Environment.NewLine +
                    "بعد از نهایی‌سازی، داده‌های این ماه قابل ویرایش نخواهند بود",
                    "Finalize Month",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes)
                    return;

                using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
                using SqliteTransaction tx = conn.BeginTransaction();

                try
                {
                    MonthlyFinalReportService.FinalizeMonthlyReport(
                        conn,
                        tx,
                        _reportProfile.StationName,
                        year,
                        month,
                        _dataStartDate,
                        Environment.UserName,
                        _currentGeneratedReportResult,
                        _currentEventReportResult,
                        _currentRecycleChangeCount);

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }

                MessageBox.Show(
                    "ماه انتخاب‌شده با موفقیت نهایی و قفل شد",
                    "Finalize Month",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                ClearGeneratedReportCache();

                _currentReportDateFrom = request.DateFrom;
                _currentReportDateTo = request.DateTo;

                LoadFinalizedMonthlyReportFromSnapshot(year, month);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در نهایی‌سازی ماه:" +
                    Environment.NewLine +
                    ex.Message,
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                UpdateReportActionButtonsState();
            }
        }

        private void btnPDF_Click(object sender, EventArgs e)
        {
            try
            {
                if (!rdoMonthly.Checked)
                {
                    MessageBox.Show(
                        " فقط برای گزارش ماهانه قابل انجام است",
                        "PDF Report",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                if (cmbYear.SelectedItem == null)
                {
                    MessageBox.Show(
                        "سال گزارش را انتخاب کنید",
                        "PDF Report",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                if (!TryGetSelectedMonthNumber(out int month))
                {
                    MessageBox.Show(
                        "ماه گزارش را انتخاب کنید",
                        "PDF Report",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                int year = Convert.ToInt32(cmbYear.SelectedItem);

                if (!MonthlyLockService.IsMonthLocked(year, month))
                {
                    MessageBox.Show(
                        "برای صدور گزارش، ابتدا باید ماه انتخاب ‌شده نهایی شود",
                        "PDF Report",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                ExportMonthlyFinalPdf(year, month);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در صدور PDF:" +
                    Environment.NewLine +
                    ex.Message,
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                UpdateReportActionButtonsState();
            }
        }

        private void btnSummaryPage_Click(object sender, EventArgs e)
        {
            SetActivePage(btnSummaryPage);
            rdoLogByEvent.Visible = false;
            rdoLogByUnit.Visible = false;
        }

        private void btnEventsPage_Click(object sender, EventArgs e)
        {
            SetActivePage(btnEventsPage);
            rdoLogByEvent.Visible = false;
            rdoLogByUnit.Visible = false;
        }

        private void btnServicePage_Click(object sender, EventArgs e)
        {
            SetActivePage(btnServicePage);
            rdoLogByEvent.Visible = false;
            rdoLogByUnit.Visible = false;
        }

        private void btnLogPage_Click(object sender, EventArgs e)
        {
            SetActivePage(btnLogPage);
            rdoLogByEvent.Visible = true;
            rdoLogByUnit.Visible = true;
        }

        #endregion

    }
}
