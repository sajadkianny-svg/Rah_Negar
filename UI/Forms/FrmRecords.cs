using Microsoft.Data.Sqlite;
using Rah_Negar.Core;
using Rah_Negar.Data;
using Rah_Negar.Models;
using Rah_Negar.Utils;
using Rah_Negar.Services;
using Rah_Negar.Services.Records;
using Rah_Negar.Services.Reports;
using ShamsiDatePickerLibrary;
using System.Reflection;
using Rah_Negar.UI.Forms.Base;
using Rah_Negar.Services.UI;

namespace Rah_Negar.UI.Forms
{


    public partial class FrmRecords : BaseForm
    {
        #region فیلدها و وضعیت داخلی فرم

        /// <summary>
        /// شماره تم فعال فرم گزارش.
        /// </summary>
        private int _currentThemeIndex;

        /// <summary>
        /// مشخص می‌کند تم کامل فرم قبلاً اعمال شده یا نه.
        /// </summary>
       // private bool _isThemeApplied;

        /// <summary>
        /// کنترل انتخاب تاریخ شمسی که به صورت Runtime داخل پنل تاریخ ساخته می‌شود
        /// این کنترل منبع اصلی تاریخ انتخابی فرم است و تمام عملیات Load و Save بر اساس مقدار آن انجام می‌شود
        /// </summary>
        private ShamsiDatePicker? datePicker;

        /// <summary>
        /// تنظیمات فعال برنامه که از دیتابیس خوانده می‌شود
        /// این تنظیمات برای تشخیص ایستگاه، پروفایل‌ها و رفتارهای وابسته به دیتابیس استفاده می‌شود
        /// </summary>
        private AppSettingsModel? _appSettings;

        /// <summary>
        /// نام ایستگاه فعال
        /// این مقدار از تنظیمات برنامه خوانده می‌شود و برای انتخاب GridProfile، PasteProfile و StationProfile استفاده می‌شود
        /// </summary>
        private string _stationName = string.Empty;

        /// <summary>
        /// پروفایل ساختار و ظاهر گرید اصلی داده‌ها
        /// این پروفایل مشخص می‌کند فرم برای ایستگاه فعال چه ستون‌هایی داشته باشد
        /// </summary>
        private GridProfile? _gridProfile;

        /// <summary>
        /// مشخص می‌کند فرم در حال بروزرسانی گروهی داده‌های گرید است.
        /// در این حالت رویدادهای سنگین مانند CellValueChanged
        /// نباید محاسبات و بروزرسانی‌های مجدد انجام دهند
        /// تا عملیات Paste با سرعت بالا انجام شود.
        /// </summary>
        private bool _isBulkUpdatingGrid;

        /// <summary>
        /// پروفایل عملیاتی ایستگاه فعال
        /// تمام منطق وابسته به نوع ایستگاه باید از طریق این شیء کنترل شود تا فرم به نام ایستگاه‌ها وابستگی مستقیم نداشته باشد
        /// </summary>
        private IStationUiProfile _stationProfile = null!;

        /// <summary>
        /// Snapshot داده‌های روزانه لودشده از tbl_data
        /// نوع واقعی این شیء برای هر ایستگاه متفاوت است و هنگام بررسی تغییرات استفاده می‌شود
        /// </summary>
        private object? _loadedDailyDataSnapshot;

        /// <summary>
        /// پروفایل Paste مربوط به ایستگاه فعال
        /// اعتبارسنجی داده‌های کپی‌شده از اکسل، تعداد ستون‌ها، تعداد ردیف‌ها و محاسبات وابسته به آن از طریق این پروفایل انجام می‌شود
        /// </summary>
        private PasteProfile? _pasteProfile;

        /// <summary>
        /// حالت فعلی فرم رکورد
        /// این مقدار تعیین می‌کند فرم در حالت خالی، داده Paste شده، داده Load شده یا ویرایش قرار دارد
        /// </summary>
        private RecordFormMode _currentMode = RecordFormMode.Empty;

        /// <summary>
        /// حالت فعلی ورود رویداد
        /// دکمه btnAdd بر اساس این مقدار یا رویداد جدید اضافه می‌کند یا تغییرات رویداد انتخاب‌شده را اعمال می‌کند
        /// </summary>
        private EventEntryMode _eventEntryMode = EventEntryMode.Add;

        /// <summary>
        /// Snapshot داده‌های tbl_unique برای تاریخ لودشده
        /// هنگام ذخیره در حالت ویرایش برای تشخیص تغییرات استفاده می‌شود
        /// </summary>
        private DailyUniqueLoadModel? _loadedUniqueRow;

        /// <summary>
        /// Snapshot رویدادهای tbl_events برای تاریخ لودشده
        /// هنگام ذخیره در حالت ویرایش برای تشخیص تغییرات استفاده می‌شود
        /// </summary>
        private List<DailyEventRowModel> _loadedEventsRows = new();

        #endregion

        #region سازنده و رویدادهای اصلی فرم

        /// <summary>
        /// سازنده فرم رکورد
        /// کنترل‌های اصلی فرم، تاریخ شمسی، گرید، ComboBoxهای رویداد، محدودیت‌های ورودی و DoubleBuffering را مقداردهی می‌کند
        /// مقداردهی نهایی وابسته به دیتابیس فعال در رویداد Load انجام می‌شود
        /// </summary>
        public FrmRecords()
        {
            InitializeComponent();
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = UiScaleService.GetDefaultFont(this, 9f);

            EnableDoubleBuffering(this);
            EnableDoubleBuffering(tabControl1);
            EnableDoubleBuffering(tabPage1);
            EnableDoubleBuffering(tabPage2);
            EnableDoubleBuffering(dgvData);
            EnableDoubleBuffering(dgvEvents);

            ApplyThemeToRecordsForm();
            InitializeShamsiDatePicker();

            string stationName = "Rasht Station";

            GridProfile profile = GridProfileProvider.GetProfile(stationName);
            ApplyGridProfileToDataGridView(profile);

            dtpTime.Format = DateTimePickerFormat.Custom;
            dtpTime.CustomFormat = "HH:mm";
            dtpTime.ShowUpDown = true;

            dgvEvents.EditMode = DataGridViewEditMode.EditProgrammatically;
            dgvEvents.ReadOnly = true;

            LoadEventComboBoxes();
            LoadUnits();

            txtRemark.MaxLength = 55;
            txtRemark.Enabled = false;

            btnEndSelection.Enabled = false;
            btnEndSelection.Visible = false;

            cmbType.SelectedIndexChanged += (_, _) => UpdateRemarkState();
            dgvData.CellValueChanged += dgvData_CellValueChanged;

            txt_irFuel.KeyPress += NumericTextBox_KeyPress;
            txt_TurbineFuel.KeyPress += NumericTextBox_KeyPress;
            txt_Flow.KeyPress += NumericTextBox_KeyPress;
            txt_nonFlow.KeyPress += NumericTextBox_KeyPress;
            txt_Vent.KeyPress += NumericTextBox_KeyPress;

            dgvData.SizeChanged += (_, _) => FitDgvDataColumnsByProfile();
            dgvEvents.SizeChanged += (_, _) => FitEventsGridColumns();

            dgvData.CellValidating += dgvData_CellValidating;

            KeyPreview = true;
            //KeyDown += FrmRecords_KeyDown;

            EnableDoubleBuffering(dgvData);
        }

        /// <summary>
        /// رویداد Load فرم
        /// فرم را بر اساس دیتابیس فعال، ایستگاه ذخیره‌شده و پروفایل‌های مرتبط آماده می‌کند
        /// </summary>
        private void FrmRecords_Load(object sender, EventArgs e)
        {
            try
            {
                ReinitializeFormByCurrentDatabase();
               // _isThemeApplied = false;
                ApplyThemeToRecordsForm();

                DataGridViewUiService.ConfigureBaseGrid(
                    dgvEvents,
                    this,
                    allowHorizontalScroll: false);

                DataGridViewUiService.SetHeaderHeight(
                    dgvEvents,
                    this,
                    baseHeight: 38);

                FitEventsGridColumns();

                SetFormMode(RecordFormMode.Empty);

            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در آماده‌سازی فرم رکورد", ex, "خطا");
            }
        }

        /// <summary>
        /// وقتی دیتابیس فعال تغییر کند، فرم را بر اساس دیتابیس و ایستگاه جدید دوباره آماده می‌کند
        /// این متد برای سناریوهای آینده مانند Switch Database قابل استفاده است
        /// </summary>
        public void RefreshForActiveDatabase()
        {
            ReinitializeFormByCurrentDatabase();
        }

        #endregion

        #region مدیریت حالت‌های فرم

        /// <summary>
        /// فرم را در حالت خالی قرار می‌دهد
        /// در این حالت کاربر می‌تواند داده وارد کند، Paste انجام دهد، رویداد اضافه کند و ذخیره اولیه انجام دهد
        /// </summary>
        private void ApplyEmptyMode()
        {
            dgvData.ReadOnly = false;

            btnPaste.Enabled = true;
            btnSave.Enabled = true;
            btnEdit.Enabled = false;
            btnReset.Enabled = true;

            btnSaveEdit.Visible = false;
            btnCancelEdit.Visible = false;
           
            if (datePicker != null)
            datePicker.Enabled = true;

            SetSummaryControlsEditable(true);
        }

        /// <summary>
        /// فرم را در حالت داده Paste شده قرار می‌دهد
        /// در این حالت داده‌ها قابل ویرایش و آماده ذخیره اولیه هستند
        /// </summary>
        private void ApplyPastedMode()
        {
            dgvData.ReadOnly = false;

            btnPaste.Enabled = true;
            btnSave.Enabled = true;
            btnEdit.Enabled = false;
            btnReset.Enabled = true;

            btnSaveEdit.Visible = false;
            btnCancelEdit.Visible = false;
            
            if (datePicker != null)
                datePicker.Enabled = true;

            SetSummaryControlsEditable(true);
        }

        /// <summary>
        /// فرم را در حالت داده لودشده قرار می‌دهد
        /// در این حالت داده‌ها فقط خواندنی هستند و کاربر برای تغییر باید وارد حالت ویرایش شود
        /// </summary>
        private void ApplyLoadedMode()
        {
            dgvData.ReadOnly = true;

            btnPaste.Enabled = false;
            btnSave.Enabled = false;
            btnEdit.Enabled = true;
            btnReset.Enabled = true;

            btnSaveEdit.Visible = false;
            btnCancelEdit.Visible = false;

            if (datePicker != null)
                datePicker.Enabled = true;

            SetSummaryControlsEditable(false);
        }

        /// <summary>
        /// فرم را در حالت ویرایش قرار می‌دهد
        /// در این حالت کاربر می‌تواند داده‌های قبلی را تغییر دهد و با دکمه ذخیره ویرایش ثبت کند
        /// </summary>
        private void ApplyEditingMode()
        {
            dgvData.ReadOnly = false;

            btnPaste.Enabled = true;
            btnEdit.Enabled = false;
            btnReset.Enabled = true;
            btnSave.Enabled = false;

            btnSaveEdit.Visible = true;
            btnSaveEdit.Enabled = true;

            btnCancelEdit.Visible = true;
            btnCancelEdit.Enabled = true;

            if (datePicker != null)
                datePicker.Enabled = false;

            SetSummaryControlsEditable(true);
        }

        /// <summary>
        /// حالت کلی فرم را تغییر می‌دهد و تمام کنترل‌های وابسته را با همان حالت هماهنگ می‌کند
        /// این متد نقطه مرکزی مدیریت رفتار فرم در حالت‌های Empty، Pasted، Loaded و Editing است
        /// </summary>
        private void SetFormMode(RecordFormMode mode)
        {
            _currentMode = mode;

            switch (mode)
            {
                case RecordFormMode.Empty:
                    ApplyEmptyMode();
                    SetEventsGridEditable(true);
                    break;

                case RecordFormMode.Pasted:
                    ApplyPastedMode();
                    SetEventsGridEditable(true);
                    break;

                case RecordFormMode.Loaded:
                    ApplyLoadedMode();
                    SetEventsGridEditable(false);
                    break;

                case RecordFormMode.Editing:
                    ApplyEditingMode();
                    SetEventsGridEditable(true);
                    break;
            }

            if (mode == RecordFormMode.Loaded)
            {
                ClearEventEntryControls();
                dgvEvents.ClearSelection();
            }

            if (!_isBulkUpdatingGrid)
            {
                EnforceCalculatedCellsLock();
                ApplyRecordsButtonTheme();
            }
        }

        /// <summary>
        /// مشخص می‌کند آیا در حالت فعلی فرم، کاربر اجازه افزودن، ویرایش یا حذف رویدادها را دارد یا نه
        /// این متد از تغییر رویدادها در حالت Loaded جلوگیری می‌کند
        /// </summary>
        private bool CanModifyEvents()
        {
            return _currentMode == RecordFormMode.Empty
                || _currentMode == RecordFormMode.Pasted
                || _currentMode == RecordFormMode.Editing;
        }

        /// <summary>
        /// کنترل‌های خلاصه روزانه را قابل ویرایش یا فقط خواندنی می‌کند
        /// این کنترل‌ها مربوط به tbl_unique هستند و باید با حالت فرم هماهنگ باشند
        /// </summary>
        private void SetSummaryControlsEditable(bool editable)
        {
            txt_irFuel.ReadOnly = !editable;
            txt_TurbineFuel.ReadOnly = !editable;
            txt_Vent.ReadOnly = !editable;
        }

        #endregion

        #region اعمال تم فرم رکورد

        /// <summary>
        /// مدیریت کلیدهای ترکیبی تغییر و ریست تم فرم گزارش.
        /// </summary>
        private void Frm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.T)
            {
                _currentThemeIndex = AppThemeManager.LoadNextTheme(_currentThemeIndex);
                AppSettingsService.SaveThemeIndex(_currentThemeIndex);

               // _isThemeApplied = false;
                ApplyThemeToRecordsForm();

                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.Shift && e.KeyCode == Keys.R)
            {
                _currentThemeIndex = 6;
                AppThemeManager.LoadThemeByIndex(_currentThemeIndex);
                AppSettingsService.SaveThemeIndex(_currentThemeIndex);

                //_isThemeApplied = false;
                ApplyThemeToRecordsForm();

                e.SuppressKeyPress = true;
            }
        }
        /// <summary>
        /// تم کلی فرم رکورد را روی پنل‌ها، دکمه‌ها، گریدها و کنترل‌های ورودی اعمال می‌کند
        /// این متد فقط ظاهر فرم را تغییر می‌دهد و نباید منطق ذخیره، بارگذاری یا محاسبات را تغییر دهد
        /// </summary>
        private void ApplyThemeToRecordsForm()
        {
            ApplyRecordsPanelTheme();
            ApplyRecordsButtonTheme();
            ApplyRecordsInputTheme();
            ApplyRecordsGridTheme(dgvData);
            ApplyRecordsGridTheme(dgvEvents);
            ApplyDgvDataCalculatedCellsTheme();
            ApplyThemeToDatePicker();
        }

        /// <summary>
        /// رنگ پنل‌های اصلی فرم رکورد را با تم فعال هماهنگ می‌کند
        /// پنل‌های Header با رنگ قوی‌تر و پنل‌های Body با رنگ آرام‌تر تنظیم می‌شوند
        /// </summary>
        private void ApplyRecordsPanelTheme()
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            BackColor = palette.FormBackColor;

            pnlButtom.BackColor = palette.ContentBackColor;
            pnlLine.BackColor = palette.DividerBackColor;

            pnlDate.BackColor = palette.HeaderBackColor;
            pnlDateText.BackColor = palette.HeaderBackColor;

            pnl_Date.BackColor = palette.HeaderBackColor;

            pnlUnique.BackColor = palette.HeaderBackColor;
            pnlEvents.BackColor = palette.HeaderBackColor;

            pnlBodyUnique.BackColor = palette.ContentBackColor;
            pnlBodyEvents.BackColor = palette.ContentBackColor;
            pnlOperation.BackColor = ControlPaint.Light(palette.ContentBackColor, 0.04f);

            tabPage1.BackColor = palette.FormBackColor;
            tabPage2.BackColor = palette.FormBackColor;
        }

        /// <summary>
        /// دکمه‌های اصلی و عملیاتی فرم رکورد را بر اساس نقش آن‌ها استایل‌دهی می‌کند
        /// دکمه‌های ثبت و بارگذاری Primary، دکمه‌های کمکی Secondary و دکمه‌های خطرناک Danger هستند
        /// </summary>
        private void ApplyRecordsButtonTheme()
        {
            Button[] buttons =
            {
                btnPaste,
                btnLoad,
                btnEdit,
                btnMissing,
                btnReset,
                btnSave,
                btnSaveEdit,
                btnCancelEdit,
                btnAdd,
                btnDeleteItem,
                btnEndSelection
            };

            foreach (Button button in buttons)
                ApplyStandardButton(button);
        }


        private static void ApplyStandardButton(Button button)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            Color backColor = palette.PrimaryButtonBackColor;
            Color hoverColor = palette.PrimaryButtonHoverColor;
            Color downColor = palette.PrimaryButtonDownColor;

            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;

            button.BackColor = backColor;
            button.ForeColor = GetReadableTextColor(backColor);

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = hoverColor;
            button.FlatAppearance.MouseDownBackColor = downColor;

            button.Font = new Font("Tahoma", 8F, FontStyle.Regular);
            button.Cursor = Cursors.Hand;
        }

        /// <summary>
        /// کنترل‌های ورودی فرم رکورد را با تم فعال هماهنگ می‌کند
        /// این بخش شامل TextBox، ComboBox و DateTimePickerهای موجود در فرم است
        /// </summary>
        private void ApplyRecordsInputTheme()
        {
            ApplyTextBoxTheme(txt_irFuel);
            ApplyTextBoxTheme(txt_TurbineFuel);
            ApplyTextBoxTheme(txt_Flow);
            ApplyTextBoxTheme(txt_nonFlow);
            ApplyTextBoxTheme(txt_Vent);
            ApplyTextBoxTheme(txtRemark);

            ApplyComboBoxTheme(cmbUnits);
            ApplyComboBoxTheme(cmbType);

            dtpTime.Font = new Font("tahoma", 8F);
        }

        /// <summary>
        /// استایل TextBox را مطابق تم فعال اعمال می‌کند
        /// </summary>
        private static void ApplyTextBoxTheme(TextBox textBox)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            textBox.BackColor = palette.CardBackColor;
            textBox.ForeColor = palette.TextPrimaryColor;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.Font = new Font("tahoma", 8F);
        }

        /// <summary>
        /// استایل ComboBox را مطابق تم فعال اعمال می‌کند
        /// </summary>
        private static void ApplyComboBoxTheme(ComboBox comboBox)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            comboBox.BackColor = palette.CardBackColor;
            comboBox.ForeColor = palette.TextPrimaryColor;
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.Font = new Font("tahoma", 8F);
        }

        /// <summary>
        /// استایل عمومی DataGridViewهای فرم رکورد را اعمال می‌کند
        /// هدر گرید مینیمال، کم‌اشباع و نزدیک به رنگ خطوط گرید نمایش داده می‌شود
        /// Selection نیز بسیار ملایم تنظیم می‌شود تا هنگام کار طولانی چشم را خسته نکند
        /// </summary>
        private static void ApplyRecordsGridTheme(DataGridView dgv)
        {
            AppThemePalette palette = AppThemeManager.CurrentPalette;

            Color headerBack = Lighten(palette.GridHeaderBackColor, 0.72f);
            Color headerLine = headerBack;

            Color selectionBack = Lighten(palette.PrimaryButtonBackColor, 0.80f);
            Color gridLine = Lighten(palette.GridLineColor, 0.35f);

            dgv.EnableHeadersVisualStyles = false;

            dgv.BackgroundColor = palette.ContentBackColor;
            dgv.BorderStyle = BorderStyle.None;

            dgv.GridColor = gridLine;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            dgv.ColumnHeadersDefaultCellStyle.BackColor = headerBack;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = palette.TextPrimaryColor;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBack;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = palette.TextPrimaryColor;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.2F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dgv.DefaultCellStyle.BackColor = palette.GridCellBackColor;
            dgv.DefaultCellStyle.ForeColor = palette.TextPrimaryColor;
            dgv.DefaultCellStyle.SelectionBackColor = selectionBack;
            dgv.DefaultCellStyle.SelectionForeColor = palette.TextPrimaryColor;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 8.2F, FontStyle.Regular);

            dgv.AlternatingRowsDefaultCellStyle.BackColor =
                Lighten(palette.GridCellBackColor, 0.35f);

            dgv.RowHeadersDefaultCellStyle.BackColor = headerLine;
            dgv.RowHeadersDefaultCellStyle.ForeColor = palette.TextPrimaryColor;
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = headerLine;
            dgv.RowHeadersDefaultCellStyle.SelectionForeColor = palette.TextPrimaryColor;
            dgv.RowHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.2F, FontStyle.Regular);
        }


        /// <summary>
        /// رنگ سلول‌های محاسباتی dgvData را مطابق تم فعال اعمال می‌کند
        /// ستون Time، ستون Ratio و سطر AVG باید از داده‌های قابل ویرایش جدا دیده شوند
        /// </summary>
        private void ApplyDgvDataCalculatedCellsTheme()
        {
            if (_gridProfile == null || _pasteProfile == null)
                return;

            if (dgvData.Columns.Count == 0 || dgvData.Rows.Count == 0)
                return;

            AppThemePalette palette = AppThemeManager.CurrentPalette;

            Color subtleBack = palette.GridLineColor;
            Color subtleFore = palette.TextPrimaryColor;
            Color hiddenFore = subtleBack;

            int firstColumnIndex = 0;
            int lastColumnIndex = dgvData.Columns.Count - 1;
            int avgRowIndex = _pasteProfile.AverageRowIndex;

            for (int r = 0; r < dgvData.Rows.Count; r++)
            {
                if (dgvData.Rows[r].IsNewRow)
                    continue;

                dgvData.Rows[r].Cells[firstColumnIndex].Style.BackColor = subtleBack;
                dgvData.Rows[r].Cells[firstColumnIndex].Style.ForeColor = subtleFore;
                dgvData.Rows[r].Cells[firstColumnIndex].Style.SelectionBackColor = subtleBack;
                dgvData.Rows[r].Cells[firstColumnIndex].Style.SelectionForeColor = subtleFore;
                dgvData.Rows[r].Cells[firstColumnIndex].Style.Alignment =
                    DataGridViewContentAlignment.MiddleCenter;

                dgvData.Rows[r].Cells[lastColumnIndex].Style.BackColor = subtleBack;
                dgvData.Rows[r].Cells[lastColumnIndex].Style.ForeColor = subtleFore;
                dgvData.Rows[r].Cells[lastColumnIndex].Style.SelectionBackColor = subtleBack;
                dgvData.Rows[r].Cells[lastColumnIndex].Style.SelectionForeColor = subtleFore;
                dgvData.Rows[r].Cells[lastColumnIndex].Style.Alignment =
                    DataGridViewContentAlignment.MiddleCenter;
            }

            if (avgRowIndex < 0 || avgRowIndex >= dgvData.Rows.Count)
                return;

            DataGridViewRow avgRow = dgvData.Rows[avgRowIndex];

            avgRow.ReadOnly = true;
            avgRow.DefaultCellStyle.BackColor = subtleBack;
            avgRow.DefaultCellStyle.ForeColor = subtleFore;
            avgRow.DefaultCellStyle.SelectionBackColor = subtleBack;
            avgRow.DefaultCellStyle.SelectionForeColor = subtleFore;
            avgRow.DefaultCellStyle.Font = new Font("Tahoma", 8F, FontStyle.Regular);
            avgRow.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            for (int c = 0; c < dgvData.Columns.Count; c++)
            {
                avgRow.Cells[c].Style.BackColor = subtleBack;
                avgRow.Cells[c].Style.ForeColor = subtleFore;
                avgRow.Cells[c].Style.SelectionBackColor = subtleBack;
                avgRow.Cells[c].Style.SelectionForeColor = subtleFore;
                avgRow.Cells[c].Style.Font = new Font("Tahoma", 8F, FontStyle.Regular);
                avgRow.Cells[c].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            foreach (int c in _gridProfile.AverageHiddenColumns)
            {
                if (c < 0 || c >= dgvData.Columns.Count)
                    continue;

                avgRow.Cells[c].Value = "";
                avgRow.Cells[c].Style.BackColor = subtleBack;
                avgRow.Cells[c].Style.ForeColor = hiddenFore;
                avgRow.Cells[c].Style.SelectionBackColor = subtleBack;
                avgRow.Cells[c].Style.SelectionForeColor = hiddenFore;
            }
        }

        private static Color Soften(Color color, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);

            int avg = (color.R + color.G + color.B) / 3;

            int r = (int)(color.R + (avg - color.R) * amount);
            int g = (int)(color.G + (avg - color.G) * amount);
            int b = (int)(color.B + (avg - color.B) * amount);

            return Color.FromArgb(r, g, b);
        }

        /// <summary>
        /// روشن‌تر کردن رنگ با ترکیب تدریجی با سفید
        /// </summary>
        private static Color Lighten(Color baseColor, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);

            int r = (int)(baseColor.R + (255 - baseColor.R) * amount);
            int g = (int)(baseColor.G + (255 - baseColor.G) * amount);
            int b = (int)(baseColor.B + (255 - baseColor.B) * amount);

            return Color.FromArgb(r, g, b);
        }


        private static Color GetReadableTextColor(Color backColor)
        {
            double brightness =
                (backColor.R * 0.299 + backColor.G * 0.587 + backColor.B * 0.114) / 255;

            return brightness < 0.55
                ? Color.White
                : Color.Black;
        }
        #endregion

        #region مقداردهی اولیه ایستگاه، تاریخ و کنترل‌ها
        /// <summary>
        /// ساخت و افزودن کنترل تاریخ شمسی به پنل تاریخ فرم رکورد
        /// </summary>
        private void InitializeShamsiDatePicker()
        {
            int pickerWidth = UiScaleService.Scale(this, 200);
            int pickerHeight = UiScaleService.Scale(this, 30);

            pnlDate.Height = pickerHeight;
            pnlDateText.Height = pickerHeight - 2;

            datePicker = new ShamsiDatePicker
            {
                Location = new Point(UiScaleService.Scale(this, 62), 0)
            };

            datePicker.SetDisplaySize(pickerWidth, pickerHeight - 2);

            ApplyThemeToDatePicker();

            pnlDate.Controls.Add(datePicker);

            lbl_Date.Text = datePicker.ShamsiDate;

            datePicker.ShamsiDateChanged += (s, e) =>
            {
                lbl_Date.Text = datePicker.ShamsiDate;
            };

            datePicker.EnterPressed += (s, e) =>
            {
                btnLoad.PerformClick();
            };

            pnl8.Top = pnlDate.Bottom;
            dgvData.Top = pnl8.Bottom + UiScaleService.Scale(this, 3);
            dgvData.Height = tabPage1.ClientSize.Height - dgvData.Top - UiScaleService.Scale(this, 8);
        }

        /// <summary>
        /// اعمال تم فعال برنامه روی کنترل تاریخ شمسی
        /// </summary>
        private void ApplyThemeToDatePicker()
        {
            if (datePicker == null)
                return;

            AppThemePalette palette = AppThemeManager.CurrentPalette;

            datePicker.ApplyTheme(
                palette.CardBackColor,
                palette.TextPrimaryColor,
                palette.GridFixedCellBackColor,
                palette.TextPrimaryColor,
                palette.GridLineColor,
                palette.GridFixedCellBackColor);
        }

        /// <summary>
        /// با فشردن Enter روی کنترل تاریخ، دکمه Load را اجرا می‌کند
        /// این رفتار ورود سریع اطلاعات روزانه را ساده‌تر می‌کند
        /// </summary>
        private void DatePicker_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            btnLoad.PerformClick();
            e.SuppressKeyPress = true;
        }

        /// <summary>
        /// مدیریت کلیدهای میانبر فرم رکورد
        /// کلیدهای جهت چپ و راست تاریخ انتخاب‌شده را جابه‌جا می‌کنند
        /// کلید Enter عملیات بارگذاری اطلاعات را اجرا می‌کند
        /// </summary>
        private void FrmRecords_KeyDown(object? sender, KeyEventArgs e)
        {
            if (datePicker == null)
                return;

            if (e.KeyCode == Keys.Left)
            {
                datePicker.MoveDateByDays(-1);
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Right)
            {
                datePicker.MoveDateByDays(1);
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                btnLoad.PerformClick();
                e.SuppressKeyPress = true;
            }
        }

        /// <summary>
        /// مدیریت کلیدهای اصلی فرم رکورد در سطح فرم
        /// این متد حتی وقتی فوکوس روی کنترل‌های داخلی مثل گرید یا تب‌پیج باشد هم کلیدها را دریافت می‌کند
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_currentMode == RecordFormMode.Editing)
                return base.ProcessCmdKey(ref msg, keyData);

            if (datePicker != null)
            {
                if (keyData == Keys.Left)
                {
                    datePicker.MoveDateByDays(-1);
                    return true;
                }

                if (keyData == Keys.Right)
                {
                    datePicker.MoveDateByDays(1);
                    return true;
                }

                if (keyData == Keys.Enter)
                {
                    btnLoad.PerformClick();
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// آیتم‌های ComboBox مربوط به نوع رویداد و واحدها را آماده می‌کند
        /// نوع رویدادها به صورت نمایشی اضافه می‌شوند و هنگام ذخیره، مقدار استاندارد دیتابیس تولید خواهد شد
        /// </summary>
        private void LoadEventComboBoxes()
        {
            cmbUnits.Items.Clear();
            cmbType.Items.Clear();

            cmbType.Items.Add("Start");
            cmbType.Items.Add("NSD");
            cmbType.Items.Add("ESD");
            cmbType.Items.Add("OH");

            if (cmbUnits.Items.Count > 0)
                cmbUnits.SelectedIndex = -1;

            if (cmbType.Items.Count > 0)
                cmbType.SelectedIndex = -1;
        }

        /// <summary>
        /// واحدهای واقعی موجود در جدول unit_runtime_base را بارگذاری می‌کند
        /// این روش باعث می‌شود تعداد واحدها بر اساس دیتابیس و پروفایل راه‌اندازی‌شده مشخص شود
        /// </summary>
        private void LoadUnits()
        {
            cmbUnits.Items.Clear();

            try
            {
                using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

                using SqliteCommand cmd = conn.CreateCommand();
                cmd.CommandText = @"
SELECT DISTINCT unit_no
FROM unit_runtime_base
ORDER BY unit_no;";

                using SqliteDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (reader["unit_no"] == DBNull.Value)
                        continue;

                    int unitNo = Convert.ToInt32(reader["unit_no"]);

                    cmbUnits.Items.Add($"Unit {unitNo}");
                }

                if (cmbUnits.Items.Count > 0)
                    cmbUnits.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در بارگذاری لیست واحدها", ex, "خطا");
            }
        }

        /// <summary>
        /// تنظیمات ایستگاه را از دیتابیس فعال می‌خواند و پروفایل‌های مورد نیاز فرم را مقداردهی می‌کند
        /// با اضافه شدن ایستگاه جدید، کافی است Providerهای مربوطه آن ایستگاه را پشتیبانی کنند
        /// </summary>
        private void InitializeStationContext()
        {
            _appSettings = AppSettingsService.GetSettings();

            if (_appSettings == null || !_appSettings.IsInitialized)
                throw new InvalidOperationException("تنظیمات برنامه به‌درستی بارگذاری نشده است");

            _stationName = _appSettings.StationName;

            _gridProfile = GridProfileProvider.GetProfile(_stationName);
            _pasteProfile = PasteProfileProvider.GetProfile(_stationName);
            _stationProfile = StationRecordProfileProvider.GetProfile(_stationName);
        }

        /// <summary>
        /// فرم را بر اساس دیتابیس فعال و پروفایل ایستگاه جاری دوباره آماده می‌کند
        /// این متد ساختار گرید را بازسازی می‌کند و برای تغییر دیتابیس فعال در آینده هم قابل استفاده است
        /// </summary>
        private void ReinitializeFormByCurrentDatabase()
        {
            InitializeStationContext();

            if (_gridProfile == null)
                throw new InvalidOperationException("پروفایل گرید در دسترس نیست");

            ApplyGridProfileToDataGridView(_gridProfile);
            dgvData.ClearSelection();
        }

        /// <summary>
        /// تاریخ انتخاب‌شده در کنترل شمسی را به عدد قابل ذخیره در دیتابیس تبدیل می‌کند
        /// مثال خروجی: 14050418
        /// </summary>
        private long GetSelectedDateRep()
        {
            if (datePicker == null)
                throw new InvalidOperationException("کنترل تاریخ شمسی مقداردهی نشده است");

            string shamsiDate = datePicker.ShamsiDate?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(shamsiDate))
                throw new InvalidOperationException("تاریخ انتخاب نشده است");

            string numericDate = shamsiDate.Replace("/", "");

            if (!long.TryParse(numericDate, out long dateRep))
                throw new InvalidOperationException("فرمت تاریخ شمسی نامعتبر است");

            return dateRep;
        }

        #endregion

        #region تنظیمات و رفتار dgvData

        private void FitDgvDataColumnsByProfile()
        {
            if (_gridProfile == null)
                return;

            if (dgvData.Columns.Count == 0)
                return;

            List<int> widths = _gridProfile.Columns
                .Select(x => x.Width)
                .ToList();

            DataGridViewUiService.FitColumnsByBaseWidths(
                dgvData,
                widths,
                minimumWidth: 25);
        }
        private void FitEventsGridColumns()
        {
            if (dgvEvents.Columns.Count == 0)
                return;

            List<int> widths =
            [
                25,   // colId
        55,   // Unit
        65,   // Type
        65,   // Time
        360   // Remark
            ];

            DataGridViewUiService.FitColumnsByBaseWidths(
                dgvEvents,
                widths,
                minimumWidth: 25);
        }

        /// <summary>
        /// پروفایل گرید ایستگاه را روی dgvData اعمال می‌کند.
        /// این متد ستون‌ها، تنظیمات رفتاری، رنگ‌های پایه، ردیف‌های داده، ستون ساعت و سطر AVG را ایجاد می‌کند.
        /// هر بار که دیتابیس یا ایستگاه تغییر کند، این متد باید دوباره اجرا شود.
        /// </summary>
        private void ApplyGridProfileToDataGridView(GridProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            dgvData.SuspendLayout();

            try
            {
                dgvData.Columns.Clear();
                dgvData.Rows.Clear();

                dgvData.AllowUserToAddRows = profile.Visual.AllowUserToAddRows;
                dgvData.AllowUserToDeleteRows = profile.Visual.AllowUserToDeleteRows;
                dgvData.AllowUserToOrderColumns = profile.Visual.AllowUserToOrderColumns;
                dgvData.AllowUserToResizeColumns = profile.Visual.AllowUserToResizeColumns;
                dgvData.AllowUserToResizeRows = profile.Visual.AllowUserToResizeRows;
                dgvData.MultiSelect = profile.Visual.MultiSelect;
                dgvData.SelectionMode = profile.Visual.SelectionMode;
                dgvData.RowHeadersVisible = profile.Visual.RowHeadersVisible;
                dgvData.EnableHeadersVisualStyles = false;
                dgvData.EditMode = profile.Visual.EditMode;
                dgvData.ReadOnly = profile.Visual.ReadOnly;

                dgvData.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                dgvData.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
                dgvData.ScrollBars = ScrollBars.Vertical;

                Color headerBack = Soften(profile.Visual.HeaderBackColor, 0.35f);

                dgvData.ColumnHeadersDefaultCellStyle.BackColor = headerBack;
                dgvData.ColumnHeadersDefaultCellStyle.ForeColor = profile.Visual.HeaderForeColor;
                dgvData.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBack;
                dgvData.ColumnHeadersDefaultCellStyle.SelectionForeColor = profile.Visual.HeaderForeColor;
                dgvData.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                dgvData.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;

                dgvData.ColumnHeadersHeightSizeMode =
                    DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

                dgvData.GridColor = profile.Visual.GridColor;
                dgvData.DefaultCellStyle.SelectionBackColor = profile.Visual.SelectionBackColor;
                dgvData.DefaultCellStyle.SelectionForeColor = profile.Visual.SelectionForeColor;

                for (int i = 0; i < profile.Columns.Count; i++)
                {
                    GridColumnProfile colProfile = profile.Columns[i];

                    DataGridViewTextBoxColumn col = new()
                    {
                        Name = colProfile.Name,
                        HeaderText = colProfile.HeaderText,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        ReadOnly = colProfile.ReadOnly,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        MinimumWidth = 25
                    };

                    col.DefaultCellStyle.Alignment = colProfile.Alignment;
                    col.DefaultCellStyle.BackColor = (i % 2 == 0)
                        ? profile.Visual.AlternateBackColor1
                        : profile.Visual.AlternateBackColor2;

                    dgvData.Columns.Add(col);
                }

                if (dgvData.Columns.Count > profile.HourColumnIndex)
                {
                    DataGridViewColumn hourCol = dgvData.Columns[profile.HourColumnIndex];
                    hourCol.ReadOnly = true;
                    hourCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    hourCol.DefaultCellStyle.BackColor = profile.Visual.HeaderBackColor;
                    hourCol.DefaultCellStyle.ForeColor = profile.Visual.HeaderForeColor;
                }

                int totalRows = profile.Visual.DataRowCount;

                if (profile.Visual.HasAverageRow)
                    totalRows += 1;

                dgvData.Rows.Add(totalRows);

                FillOddHoursInFirstColumn(profile);

                if (profile.Visual.HasAverageRow &&
                    dgvData.Rows.Count > profile.Visual.AverageRowIndex)
                {
                    int avgRowIndex = profile.Visual.AverageRowIndex;
                    DataGridViewRow avgRow = dgvData.Rows[avgRowIndex];

                    avgRow.Cells[profile.HourColumnIndex].Value = "AVG";
                    avgRow.ReadOnly = true;
                    avgRow.DefaultCellStyle.BackColor = Color.LightSteelBlue;
                    avgRow.DefaultCellStyle.ForeColor = Color.Black;
                    avgRow.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                    foreach (int c in profile.AverageHiddenColumns)
                    {
                        if (c < 0 || c >= dgvData.Columns.Count)
                            continue;

                        avgRow.Cells[c].Value = "";
                        avgRow.Cells[c].Style.BackColor = dgvData.GridColor;
                        avgRow.Cells[c].Style.ForeColor = dgvData.GridColor;
                        avgRow.Cells[c].Style.SelectionBackColor = dgvData.GridColor;
                        avgRow.Cells[c].Style.SelectionForeColor = dgvData.GridColor;
                    }
                }

                dgvData.ReadOnly = false;

                EnableDoubleBuffering(dgvData);

                // تم عمومی گرید
                ApplyRecordsGridTheme(dgvData);

                // تنظیمات DPI و Header بعد از Theme اعمال شود تا Theme آن را خراب نکند
                DataGridViewUiService.ConfigureBaseGrid(
                    dgvData,
                    this,
                    allowHorizontalScroll: false);

                DataGridViewUiService.SetHeaderHeight(
                    dgvData,
                    this,
                    baseHeight: 58);

                FitDgvDataColumnsByProfile();

                EnforceCalculatedCellsLock();
                ApplyDgvDataCalculatedCellsTheme();
                dgvData.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.2f, FontStyle.Bold, GraphicsUnit.Point);
                dgvData.ClearSelection();
            }
            finally
            {
                dgvData.ResumeLayout();
            }
        }

        /// <summary>
        /// ستون‌ها و ردیف‌های محاسباتی گرید اصلی را قفل می‌کند
        /// ستون ساعت، ستون Ratio و سطر AVG نباید توسط کاربر تغییر مستقیم داشته باشند
        /// </summary>
        private void EnforceCalculatedCellsLock()
        {
            if (_gridProfile == null || _pasteProfile == null)
                return;

            if (_gridProfile.HourColumnIndex >= 0 &&
                _gridProfile.HourColumnIndex < dgvData.Columns.Count)
            {
                dgvData.Columns[_gridProfile.HourColumnIndex].ReadOnly = true;
            }

            if (_gridProfile.RatioColumnIndex >= 0 &&
                _gridProfile.RatioColumnIndex < dgvData.Columns.Count)
            {
                dgvData.Columns[_gridProfile.RatioColumnIndex].ReadOnly = true;
            }

            if (_pasteProfile.AverageRowIndex >= 0 &&
                _pasteProfile.AverageRowIndex < dgvData.Rows.Count)
            {
                dgvData.Rows[_pasteProfile.AverageRowIndex].ReadOnly = true;
            }

            ApplyDgvDataCalculatedCellsTheme();
        }


        /// <summary>
        /// ساعات فرد شبانه‌روز را در ستون ساعت قرار می‌دهد
        /// این ستون مبنای ثبت دوازده رکورد روزانه در ساعات 01 تا 23 است
        /// </summary>
        private void FillOddHoursInFirstColumn(GridProfile profile)
        {
            int hour = 1;

            for (int i = 0; i < profile.Visual.DataRowCount && i < dgvData.Rows.Count; i++)
            {
                dgvData.Rows[i].Cells[profile.HourColumnIndex].Value = hour.ToString("00") + ":00";
                hour += 2;
            }

            if (profile.Visual.HasAverageRow && dgvData.Rows.Count > profile.Visual.AverageRowIndex)
            {
                dgvData.Rows[profile.Visual.AverageRowIndex].Cells[profile.HourColumnIndex].Value = "AVG";
            }

            ApplyDgvDataCalculatedCellsTheme();
        }

        /// <summary>
        /// ارتفاع dgvData را بر اساس ارتفاع Header و سطرهای قابل مشاهده تنظیم می‌کند
        /// هدف این است که گرید بدون فضای خالی اضافه و بدون اسکرول غیرضروری نمایش داده شود
        /// </summary>
        private void AdjustGridHeight()
        {
            dgvData.Height = dgvData.ColumnHeadersHeight
                             + dgvData.Rows.GetRowsHeight(DataGridViewElementStates.Visible)
                             + dgvData.Margin.Vertical - 4;
        }

        /// <summary>
        /// DoubleBuffering را برای DataGridView فعال می‌کند
        /// این کار با Reflection انجام می‌شود چون پراپرتی DoubleBuffered در DataGridView به صورت عمومی در دسترس نیست
        /// </summary>
        private static void EnableDoubleBuffering(DataGridView dgv)
        {
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null,
                dgv,
                new object[] { true });
        }

        /// <summary>
        /// هنگام تغییر مقدار سلول‌های dgvData، ستون Ratio، سطر AVG و Flowها را دوباره محاسبه می‌کند
        /// این متد باعث می‌شود داده‌های محاسباتی همیشه با مقدارهای فعلی گرید هماهنگ باشند
        /// </summary>
        private void dgvData_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (_isBulkUpdatingGrid)
                    return;

                if (_pasteProfile == null || _gridProfile == null)
                    return;

                if (e.RowIndex < 0)
                    return;

                if (e.RowIndex == _pasteProfile.AverageRowIndex)
                    return;

                RecalculateRatioColumn(_pasteProfile);
                CalculateAverageRow(_pasteProfile);
                CalculateFlows(_pasteProfile);
                EnforceCalculatedCellsLock();
            }
            catch
            {
            }
        }
        /// <summary>
        /// اعتبارسنجی مستقیم سلول‌های dgvData هنگام ویرایش دستی
        /// ستون‌های Status فقط S، M، A و OH می‌پذیرند و سایر ستون‌ها باید عددی باشند
        /// </summary>
        private void dgvData_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            try
            {
                if (_pasteProfile == null)
                    return;

                if (e.RowIndex < 0)
                    return;

                if (e.RowIndex == _pasteProfile.AverageRowIndex)
                    return;

                if (e.ColumnIndex == _pasteProfile.HourGridColumnIndex)
                    return;

                string value = e.FormattedValue?.ToString()?.Trim().ToUpperInvariant() ?? "";

                if (_pasteProfile.UnitStatusGridColumns.Contains(e.ColumnIndex))
                {
                    string[] allowedStatuses = { "S", "M", "A", "OH" };

                    if (!allowedStatuses.Contains(value))
                    {
                        UiMessageService.ShowWarning(
                            "در ستون‌های Status فقط S , M , A , OH مجاز است",
                            "اعتبارسنجی");

                        e.Cancel = true;
                    }

                    return;
                }

                if (!TryParseStrictDouble(value, out _))
                {
                    UiMessageService.ShowWarning(
                        UiMessageService.Paragraphs(
                            "در این ستون فقط مقدار عددی مجاز است.",
                            "توجه! برای اعداد اعشاری فقط از نقطه استفاده شود"),
                        "اعتبارسنجی");

                    e.Cancel = true;
                }
            }
            catch
            {
            }
        }

        private static void EnableDoubleBuffering(Control control)
        {
            typeof(Control).InvokeMember(
                "DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null,
                control,
                new object[] { true });
        }
        #endregion

        #region Paste و محاسبات گرید اصلی

        /// <summary>
        /// داده کپی‌شده از اکسل را پیش از ورود به گرید اعتبارسنجی می‌کند
        /// این اعتبارسنجی بر اساس PasteProfile انجام می‌شود و تعداد ردیف، تعداد ستون، ستون‌های Status و ستون‌های عددی را بررسی می‌کند
        /// </summary>

        private static PasteValidationResult ValidateExcelPastedTable(
            List<string[]>? table,
            PasteProfile? profile)
        {
            PasteValidationResult result = new()
            {
                IsValid = false
            };

            if (table == null || profile == null)
            {
                result.Message = UiMessageService.Paragraphs(
                    "داده Paste شده یا پروفایل خواندن اطلاعات معتبر نیست.",
                    "لطفاً فایل Excel و تنظیمات پروفایل ایستگاه را بررسی کنید.");

                return result;
            }

            if (table.Count != profile.ExpectedRows)
            {
                result.Message = UiMessageService.Paragraphs(
                    "تعداد سطرهای داده Paste شده با ساختار مورد انتظار هماهنگ نیست.",
                    $"تعداد سطر مورد انتظار: {profile.ExpectedRows}",
                    $"تعداد سطر دریافت‌شده: {table.Count}");

                return result;
            }

            for (int r = 0; r < table.Count; r++)
            {
                if (table[r] == null)
                {
                    result.RowIndex = r;

                    result.Message = UiMessageService.Paragraphs(
                        $"سطر {r + 1} قابل خواندن نیست.",
                        "ساختار داده Paste شده معتبر نیست.");

                    return result;
                }

                if (table[r].Length == profile.ExpectedColumns)
                    continue;

                result.RowIndex = r;

                result.Message = UiMessageService.Paragraphs(
                    $"تعداد ستون‌های داده در سطر {r + 1} نامعتبر است.",
                    $"تعداد ستون مورد انتظار: {profile.ExpectedColumns}",
                    $"تعداد ستون دریافت‌شده: {table[r].Length}");

                return result;
            }

            HashSet<int> statusColumns =
                new(profile.StatusSourceColumns);

            HashSet<string> validStatuses =
                new(
                    profile.AllowedStatuses,
                    StringComparer.OrdinalIgnoreCase);

            HashSet<int> numericColumns =
                new(profile.NumericSourceColumns);

            for (int r = 0; r < table.Count; r++)
            {
                for (int c = 0; c < table[r].Length; c++)
                {
                    string cellValue =
                        table[r][c]?.Trim() ?? string.Empty;

                    if (statusColumns.Contains(c))
                    {
                        if (!string.IsNullOrWhiteSpace(cellValue) &&
                            validStatuses.Contains(cellValue))
                        {
                            continue;
                        }

                        result.RowIndex = r;
                        result.ColumnIndex = c;

                        result.Message = UiMessageService.Paragraphs(
                            $"مقدار واردشده در سطر {r + 1}، ستون {c + 1} معتبر نیست.",
                            "در این ستون فقط وضعیت‌های زیر مجاز هستند:",
                            string.Join(" , ", profile.AllowedStatuses));

                        return result;
                    }

                    if (numericColumns.Contains(c))
                    {
                        bool isValidNumber =
                            double.TryParse(
                                cellValue,
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out _);

                        if (!string.IsNullOrWhiteSpace(cellValue) &&
                            isValidNumber)
                        {
                            continue;
                        }

                        result.RowIndex = r;
                        result.ColumnIndex = c;

                        result.Message = UiMessageService.Paragraphs(
                            $"مقدار واردشده در سطر {r + 1}، ستون {c + 1} معتبر نیست.",
                            "در این ستون فقط مقدار عددی مجاز است.",
                            "برای اعداد اعشاری فقط از نقطه استفاده کنید.",
                            "مثال صحیح: 12.5");

                        return result;
                    }

                    result.RowIndex = r;
                    result.ColumnIndex = c;

                    result.Message = UiMessageService.Paragraphs(
                        $"ستون {c + 1} در پروفایل Paste تعریف نشده است.",
                        "ساختار داده Paste شده با پروفایل ایستگاه فعال هماهنگ نیست.");

                    return result;
                }
            }

            result.IsValid = true;

            return result;
        }

        /// <summary>
        /// دکمه Paste داده‌های کپی‌شده از اکسل را دریافت، اعتبارسنجی و وارد dgvData می‌کند
        /// پس از Paste، محاسبات Ratio، AVG و Flowها انجام می‌شود و فرم وارد حالت Pasted خواهد شد
        /// </summary>

        private void btnPaste_Click(object sender, EventArgs e)
        {
            try
            {
                if (_pasteProfile == null || _gridProfile == null)
                {
                    UiMessageService.ShowError(
                        "پروفایل ایستگاه بارگذاری نشده است",
                        "خطا");

                    return;
                }

                if (!Clipboard.ContainsText())
                {
                    UiMessageService.ShowWarning(
                        "هیچ داده‌ای در کلیپ‌بورد یافت نشد",
                        "هشدار");

                    return;
                }

                string clipboardText = Clipboard.GetText();

                if (string.IsNullOrWhiteSpace(clipboardText))
                {
                    UiMessageService.ShowWarning(
                        "داده موجود در کلیپ‌بورد خالی است",
                        "هشدار");

                    return;
                }

                List<string[]> table = clipboardText
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Trim()
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line =>
                        line.Split('\t')
                            .Select(cell => cell.Trim())
                            .ToArray())
                    .Where(row => row.Length > 0)
                    .ToList();

                PasteValidationResult validation =
                    ValidateExcelPastedTable(table, _pasteProfile);

                if (!validation.IsValid)
                {
                    UiMessageService.ShowWarning(
                        validation.Message,
                        "خطای Paste");

                    return;
                }

                int requiredRows = _pasteProfile.ExpectedRows + 1;

                if (dgvData.Rows.Count < requiredRows)
                {
                    UiMessageService.ShowError(
                        "ساختار جدول آماده نیست.",
                        "خطا");

                    return;
                }

                _isBulkUpdatingGrid = true;
                dgvData.SuspendLayout();

                try
                {
                    ClearMainDataRows(_pasteProfile);
                    PasteTableIntoGrid(table, _pasteProfile);

                    RecalculateRatioColumn(_pasteProfile);
                    FillOddHoursInFirstColumn(_gridProfile);
                    CalculateAverageRow(_pasteProfile);
                    CalculateFlows(_pasteProfile);
                    EnforceCalculatedCellsLock();

                    dgvData.ClearSelection();
                }
                finally
                {
                    dgvData.ResumeLayout();
                    _isBulkUpdatingGrid = false;
                }

                SetFormMode(RecordFormMode.Pasted);
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError(
                    "خطا در پردازش داده‌های Paste شده",
                    ex,
                    "خطا");
            }
        }

        /// <summary>
        /// ردیف‌های اصلی داده را پاک می‌کند ولی ساختار گرید، ستون ساعت و سطر AVG را حفظ می‌کند
        /// این متد قبل از Paste داده جدید استفاده می‌شود
        /// </summary>
        private void ClearMainDataRows(PasteProfile profile)
        {
            for (int r = 0; r < profile.ExpectedRows && r < dgvData.Rows.Count; r++)
            {
                for (int c = profile.GridStartColumn; c < dgvData.Columns.Count; c++)
                    dgvData.Rows[r].Cells[c].Value = "";
            }
        }

        /// <summary>
        /// داده‌های اعتبارسنجی‌شده Clipboard را داخل dgvData قرار می‌دهد
        /// مقداردهی از GridStartColumn شروع می‌شود چون ستون‌های ابتدایی مانند ساعت معمولاً سیستمی هستند
        /// </summary>
        private void PasteTableIntoGrid(List<string[]> table, PasteProfile profile)
        {
            for (int r = 0; r < profile.ExpectedRows && r < table.Count; r++)
            {
                for (int c = 0; c < profile.ExpectedColumns && c < table[r].Length; c++)
                {
                    int targetGridColumn = profile.GridStartColumn + c;

                    if (targetGridColumn >= dgvData.Columns.Count)
                        continue;

                    dgvData.Rows[r].Cells[targetGridColumn].Value = table[r][c].Trim().ToUpper();
                }
            }
        }

        /// <summary>
        /// ستون Ratio را برای ردیف‌های اصلی محاسبه می‌کند
        /// محاسبه بر اساس ستون‌های فشار ورودی و خروجی تعریف‌شده در PasteProfile انجام می‌شود
        /// </summary>
        private void RecalculateRatioColumn(PasteProfile profile)
        {
            int maxRows = Math.Min(profile.ExpectedRows, dgvData.Rows.Count);

            for (int r = 0; r < maxRows; r++)
            {
                string? firstCell = dgvData.Rows[r].Cells[profile.HourGridColumnIndex].Value?.ToString();

                if (string.Equals(firstCell?.Trim(), "AVG", StringComparison.OrdinalIgnoreCase))
                    continue;

                double inP = ConvertToDouble(dgvData.Rows[r].Cells[profile.RatioSourceInGridColumn].Value);
                double outP = ConvertToDouble(dgvData.Rows[r].Cells[profile.RatioSourceOutGridColumn].Value);

                double result = inP != 0 ? outP / inP : 0;

                dgvData.Rows[r].Cells[profile.RatioTargetGridColumn].Value =
                    result == 0 ? "0" : result.ToString("F2");
            }
        }

        /// <summary>
        /// سطر AVG را برای ستون‌های مشخص‌شده در PasteProfile محاسبه می‌کند
        /// ستون‌هایی که در لیست AverageGridColumns نیستند در سطر AVG خالی می‌مانند
        /// </summary>
        private void CalculateAverageRow(PasteProfile profile)
        {
            if (dgvData.Rows.Count <= profile.AverageRowIndex)
                return;

            DataGridViewRow avgRow = dgvData.Rows[profile.AverageRowIndex];

            for (int c = 0; c < dgvData.Columns.Count; c++)
            {
                if (profile.AverageGridColumns.Contains(c))
                {
                    double sum = 0;
                    int count = 0;

                    for (int r = 0; r < profile.ExpectedRows; r++)
                    {
                        object? cellValue = dgvData.Rows[r].Cells[c].Value;

                        if (!IsValidNumericValue(cellValue?.ToString()))
                            continue;

                        sum += ConvertToDouble(cellValue);
                        count++;
                    }

                    avgRow.Cells[c].Value = count > 0
                        ? Math.Round(sum / count, 1).ToString("F1")
                        : "";

                }
                else
                {
                    avgRow.Cells[c].Value = "";
                }
            }

            avgRow.Cells[profile.HourGridColumnIndex].Value = "AVG";
            avgRow.ReadOnly = true;

            ApplyDgvDataCalculatedCellsTheme();
        }

        /// <summary>
        /// میانگین Flow توربینی و غیرتوربینی را بر اساس وضعیت واحدها محاسبه می‌کند
        /// اگر حداقل یک واحد در وضعیت S باشد، Flow آن ردیف توربینی محسوب می‌شود
        /// </summary>
        private void CalculateFlows(PasteProfile profile)
        {
            double turbineFlow = 0;
            double nonTurbineFlow = 0;
            int turbineCount = 0;
            int nonTurbineCount = 0;

            for (int r = 0; r < profile.ExpectedRows; r++)
            {
                double flow = ConvertToDouble(dgvData.Rows[r].Cells[profile.FlowGridColumn].Value);

                bool anyUnitInS = false;

                foreach (int statusCol in profile.UnitStatusGridColumns)
                {
                    string status = Convert.ToString(dgvData.Rows[r].Cells[statusCol].Value)?.Trim() ?? "";

                    if (status != "S")
                        continue;

                    anyUnitInS = true;
                    break;
                }

                if (anyUnitInS)
                {
                    turbineFlow += flow;
                    turbineCount++;
                }
                else
                {
                    nonTurbineFlow += flow;
                    nonTurbineCount++;
                }
            }

            turbineFlow = turbineCount > 0 ? Math.Round(turbineFlow / turbineCount, 1) : 0;
            nonTurbineFlow = nonTurbineCount > 0 ? Math.Round(nonTurbineFlow / nonTurbineCount, 1) : 0;

            txt_Flow.Text = turbineFlow.ToString("F1");
            txt_nonFlow.Text = nonTurbineFlow.ToString("F1");
        }

        #endregion

        #region اعتبارسنجی و تبدیل مقدارها


        /// <summary>
        /// هنگام ثبت یا تغییر رویداد OH، هشدار جدی به کاربر نمایش داده می‌شود
        /// </summary>
        private static bool ConfirmOverhaulEvent()
        {
            string message = UiMessageService.Paragraphs(
                "ثبت این رویداد باعث ریست شدن کارکرد بعد از اورهال برای این واحد می‌شود.",
                "از این تاریخ به بعد آیتم زیر در گزارش‌ها از صفر محاسبه خواهد شد:" +
                Environment.NewLine +
                "[Runtime After OH]",
                "آیا ادامه می‌دهید؟");

            return UiMessageService.ConfirmWarning(
                message,
                "هشدار رویداد OH");
        }


        /// <summary>
        /// قبل از ذخیره، کامل بودن و معتبر بودن داده‌های اصلی فرم را بررسی می‌کند
        /// این متد dgvData، مقادیر خلاصه روزانه و قوانین پایه ثبت را کنترل می‌کند
        /// </summary>
        private bool ValidateBeforeSave()
        {
            if (_pasteProfile == null)
                return false;

            for (int r = 0; r < _pasteProfile.ExpectedRows; r++)
            {
                for (int c = 0; c < dgvData.Columns.Count; c++)
                {
                    if (c == _pasteProfile.HourGridColumnIndex)
                        continue;

                    object? cellObj = dgvData.Rows[r].Cells[c].Value;
                    string value = cellObj?.ToString()?.Trim().ToUpper() ?? "";

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        UiMessageService.ShowWarning($"سلول سطر {r + 1} و ستون {c + 1} خالی است", "اعتبارسنجی");

                        dgvData.CurrentCell = dgvData.Rows[r].Cells[c];
                        dgvData.BeginEdit(true);
                        return false;
                    }

                    if (_pasteProfile.UnitStatusGridColumns.Contains(c))
                    {
                        string[] allowedStatuses = { "S", "M", "A", "OH" };

                        if (allowedStatuses.Contains(value))
                            continue;
                        UiMessageService.ShowWarning($"در سطر {r + 1} و ستون {c + 1} فقط مقادیر S , M , A , OH مجاز است", "اعتبارسنجی");

                        dgvData.CurrentCell = dgvData.Rows[r].Cells[c];
                        dgvData.BeginEdit(true);
                        return false;
                    }

                    if (TryParseStrictDouble(value, out _))
                        continue;

                    UiMessageService.ShowWarning(
                        UiMessageService.Paragraphs(
                            $"مقدار سطر {r + 1} و ستون {c + 1} باید عددی باشد.",
                            "برای اعداد اعشاری فقط از نقطه استفاده کنید.", "اعتبارسنجی"));

                    dgvData.CurrentCell = dgvData.Rows[r].Cells[c];
                    dgvData.BeginEdit(true);
                    return false;
                }
            }

            return ValidateSummaryFields();
        }

        /// <summary>
        /// بررسی می‌کند مقادیر ضروری بخش خلاصه روزانه وارد شده باشند
        /// این بخش به جدول tbl_unique مربوط است و بدون آن ثبت کامل روزانه معتبر نیست
        /// </summary>
        private bool ValidateSummaryFields()
        {
            if (string.IsNullOrWhiteSpace(txt_irFuel.Text) ||
                string.IsNullOrWhiteSpace(txt_TurbineFuel.Text) ||
                string.IsNullOrWhiteSpace(txt_Flow.Text) ||
                string.IsNullOrWhiteSpace(txt_nonFlow.Text))
            {
                UiMessageService.ShowWarning(
                    "داده‌های بخش مصرف سوخت را کامل وارد نمایید",
                    "اعتبارسنجی");

                return false;
            }

            if (string.IsNullOrWhiteSpace(txt_Vent.Text))
            {
                txt_Vent.Text = "0";
            }

            return true;
        }

        /// <summary>
        /// ورود کاراکتر در TextBoxهای عددی را محدود می‌کند
        /// فقط عدد، کنترل‌کاراکترها و یک نقطه اعشار مجاز است
        /// </summary>
        private void NumericTextBox_KeyPress(object? sender, KeyPressEventArgs e)
        {
            try
            {
                if (char.IsControl(e.KeyChar))
                    return;

                if (sender is not TextBox txt)
                {
                    e.Handled = true;
                    return;
                }

                if (char.IsDigit(e.KeyChar))
                    return;

                if (e.KeyChar == '.' && !txt.Text.Contains('.'))
                    return;

                e.Handled = true;
            }
            catch
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// بررسی می‌کند مقدار متنی ورودی یک عدد معتبر باشد
        /// برای محاسبات AVG و تبدیل داده‌های گرید استفاده می‌شود
        /// </summary>
        private static bool IsValidNumericValue(string? value)
        {
            return TryParseStrictDouble(value, out _);
        }

        private static bool TryParseStrictDouble(string? text, out double value)
        {
            value = 0;

            string raw = (text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            if (raw.Contains(','))
                return false;

            return double.TryParse(
                raw,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }


        /// <summary>
        /// مقدار شیء ورودی را به عدد اعشاری تبدیل می‌کند
        /// اگر مقدار نامعتبر باشد صفر برمی‌گرداند تا محاسبات فرم متوقف نشود
        /// </summary>
        private static double ConvertToDouble(object? value)
        {
            string text = value?.ToString()?.Trim() ?? "";

            if (!TryParseStrictDouble(text, out double result))
                throw new InvalidOperationException(
                    $"مقدار عددی نامعتبر است: {text}");

            return result;
        }

        /// <summary>
        /// مقدار شیء ورودی را به double nullable تبدیل می‌کند
        /// برای ساخت DTOهای ذخیره‌سازی از داده‌های dgvData استفاده می‌شود
        /// </summary>
        private static double? TryGetDouble(object? val)
        {
            string text = val?.ToString()?.Trim() ?? "";

            if (!TryParseStrictDouble(text, out double result))
                throw new InvalidOperationException(
                    $"مقدار عددی نامعتبر است: {text}");

            return result;
        }


        /// <summary>
        /// مقدار شیء ورودی را به int nullable تبدیل می‌کند
        /// برای RPM واحدها در DTOهای ذخیره‌سازی استفاده می‌شود
        /// </summary>
        private static int? TryGetInt(object? val)
        {
            return int.TryParse(val?.ToString(), out int i) ? i : null;
        }

        #endregion

        #region ذخیره و ویرایش داده‌ها

        /// <summary>
        /// عملیات مشترک ذخیره اولیه و ذخیره تغییرات را انجام می‌دهد
        /// ثبت جدید فقط به ترتیب تاریخ مجاز است
        /// ویرایش فقط به صورت جایگزینی داده‌های همان تاریخ ثبت‌شده انجام می‌شود
        /// </summary>
        private void ExecuteSave(bool isEditMode)
        {
            try
            {
                if (_stationProfile == null)
                    throw new InvalidOperationException("پروفایل ایستگاه مقداردهی نشده است");

                if (!ValidateBeforeSave())
                    return;

                long dateRep = GetSelectedDateRep();

                if (IsSelectedMonthLocked(dateRep))
                    return;


                if (!AppSettingsService.IsDateAllowedByDataStartDate(dateRep))
                {
                    UiMessageService.ShowWarning(AppSettingsService.BuildDataStartDateViolationMessage(dateRep), "تاریخ غیرمجاز");

                    return;
                }

                DailySaveSequenceResult sequenceResult = isEditMode
                    ? DailySaveSequenceService.ValidateEdit(dateRep)
                    : DailySaveSequenceService.ValidateNewSave(dateRep);

                if (!sequenceResult.IsValid)
                {
                    UiMessageService.ShowWarning(sequenceResult.Message, "اعتبارسنجی ترتیب ثبت");

                    return;
                }

                if (isEditMode && !HasAnyChanges())
                {
                    UiMessageService.ShowInfo("تغییری انجام نشده است", "اطلاع");

                    return;
                }

                if (isEditMode)
                {
                    if (!UiMessageService.ConfirmWarning(
                            "داده‌های ثبت‌شده این تاریخ با مقادیر جدید جایگزین می‌شود، ادامه می‌دهید؟",
                            "تأیید ویرایش"))
                    {
                        return;
                    }
                }

                DailyUniqueSaveModel uniqueModel = BuildDailyUniqueSaveModel();
                List<DailyEventRowModel> eventsModel = BuildDailyEventsSaveModel();

                EventSequenceValidationResult eventValidation =
                    EventSequenceValidationService.ValidateDailyEvents(dateRep, eventsModel);

                if (!eventValidation.IsValid)
                    return;

                using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
                using SqliteTransaction tx = conn.BeginTransaction();

                try
                {
                    InsertStationDailyData(conn, tx, dateRep);

                    CommonRecordPersistenceService.DeleteExistingUnique(conn, tx, dateRep);
                    CommonRecordPersistenceService.InsertUnique(conn, tx, uniqueModel);

                    CommonRecordPersistenceService.DeleteExistingEvents(conn, tx, dateRep);
                    CommonRecordPersistenceService.InsertEvents(conn, tx, eventsModel);

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }

                UiMessageService.ShowSuccess(isEditMode ? "تغییرات با موفقیت ذخیره شد" : "اطلاعات با موفقیت ذخیره شد", "موفق");

                if (isEditMode)
                {
                    LoadCurrentDateData();
                    SetFormMode(RecordFormMode.Loaded);
                }
                else
                {
                    ResetRecordForm(false);
                }
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در ذخیره اطلاعات", ex, "خطا");
            }
        }


        /// <summary>
        /// دکمه Save ثبت اولیه اطلاعات روزانه را انجام می‌دهد
        /// این دکمه فقط زمانی فعال است که فرم در حالت Pasted قرار داشته باشد
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (_currentMode != RecordFormMode.Pasted)
                    return;

                ExecuteSave(false);
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در ذخیره اطلاعات", ex, "خطا");

            }
        }


        /// <summary>
        /// دکمه ذخیره ویرایش، داده‌های لودشده همان تاریخ را جایگزین می‌کند
        /// </summary>
        private void btnSaveEdit_Click(object sender, EventArgs e)
        {
            try
            {
                if (_currentMode != RecordFormMode.Editing)
                    return;

                ExecuteSave(true);
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در ذخیره اطلاعات", ex, "خطا");

            }
        }


        /// <summary>
        /// فرم را وارد حالت ویرایش می‌کند
        /// این عملیات فقط وقتی مجاز است که داده‌ای از دیتابیس لود شده باشد
        /// </summary>
        private void btnEdit_Click(object sender, EventArgs e)
        {
            try
            {
                if (_currentMode != RecordFormMode.Loaded)
                    return;

                long dateRep = GetSelectedDateRep();

                if (IsSelectedMonthLocked(dateRep))
                    return;

                if (!CommonRecordQueryService.ExistsForDate(dateRep))
                {
                    UiMessageService.ShowWarning("برای این تاریخ داده‌ای ثبت نشده است و امکان ویرایش وجود ندارد", "ویرایش");

                    return;
                }

                SetFormMode(RecordFormMode.Editing);
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در ورود به حالت ویرایش", ex, "خطا");

            }
        }

        /// <summary>
        /// ویرایش را لغو می‌کند و داده‌های همان تاریخ را دوباره از دیتابیس بارگذاری می‌کند
        /// این کار باعث از بین رفتن تغییرات ذخیره‌نشده کاربر می‌شود
        /// </summary>
        private void btnCancelEdit_Click(object sender, EventArgs e)
        {
            try
            {
                if (_currentMode != RecordFormMode.Editing)
                    return;

                string message = UiMessageService.Paragraphs(
                    "تغییرات ذخیره‌نشده این تاریخ از بین می‌رود.",
                    "اطلاعات قبلی دوباره از دیتابیس بارگذاری خواهد شد.",
                    "آیا می‌خواهید ویرایش را لغو کنید؟");

                if (!UiMessageService.ConfirmWarning(message, "لغو ویرایش"))
                    return;

                LoadCurrentDateData();
                SetFormMode(RecordFormMode.Loaded);
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در لغو ویرایش", ex, "خطا");
            }
        }

        /// <summary>
        /// فرم را به وضعیت اولیه برمی‌گرداند
        /// در صورت نیاز، قبل از ریست از کاربر تأیید گرفته می‌شود تا تغییرات واردنشده از بین نرود
        /// </summary>
        private void ResetRecordForm(bool askConfirmation)
        {
            try
            {
                bool needConfirm =
                    askConfirmation &&
                    (_currentMode == RecordFormMode.Editing ||
                     _currentMode == RecordFormMode.Pasted);

                if (needConfirm)
                {
                    string message = UiMessageService.Paragraphs(
                        "تمام تغییرات فعلی از بین می‌رود.",
                        "آیا می‌خواهید فرم را ریست کنید؟");

                    if (!UiMessageService.ConfirmWarning(message, "تأیید ریست"))
                        return;
                }

                _isBulkUpdatingGrid = true;

                dgvData.SuspendLayout();
                dgvEvents.SuspendLayout();

                try
                {
                    if (_pasteProfile != null)
                    {
                        ClearMainDataRows(_pasteProfile);
                        ClearAverageRow();
                    }


                    if (_gridProfile != null)
                        FillOddHoursInFirstColumn(_gridProfile);

                    txt_irFuel.Clear();
                    txt_TurbineFuel.Clear();
                    txt_Flow.Clear();
                    txt_nonFlow.Clear();
                    txt_Vent.Clear();

                    dgvEvents.Rows.Clear();

                    _loadedDailyDataSnapshot = null;
                    _loadedUniqueRow = null;
                    _loadedEventsRows.Clear();

                    ClearEventEntryControls();

                    dgvData.ClearSelection();
                    dgvEvents.ClearSelection();
                    dgvEvents.CurrentCell = null;
                }
                finally
                {
                    dgvEvents.ResumeLayout();
                    dgvData.ResumeLayout();

                    _isBulkUpdatingGrid = false;
                }

                SetFormMode(RecordFormMode.Empty);
            }
            catch (Exception ex)
            {
                _isBulkUpdatingGrid = false;

                UiMessageService.ShowError(
                    "خطا در ریست فرم",
                    ex,
                    "خطا");
            }
        }

        /// <summary>
        /// دکمه Reset فرم را با تأیید کاربر به وضعیت خالی برمی‌گرداند
        /// </summary>
        /// 
        private void ClearAverageRow()
        {
            if (_pasteProfile == null)
                return;

            int avgRowIndex = _pasteProfile.AverageRowIndex;

            if (avgRowIndex < 0 || avgRowIndex >= dgvData.Rows.Count)
                return;

            for (int c = 0; c < dgvData.Columns.Count; c++)
            {
                dgvData.Rows[avgRowIndex].Cells[c].Value = "";
            }

            dgvData.Rows[avgRowIndex]
                .Cells[_pasteProfile.HourGridColumnIndex]
                .Value = "AVG";
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            ResetRecordForm(true);
        }

        /// <summary>
        /// بررسی می‌کند آیا داده‌های فعلی فرم با Snapshot لودشده از دیتابیس تفاوت دارد یا نه
        /// این بررسی شامل tbl_data، tbl_unique و tbl_events است
        /// </summary>
        private bool HasAnyChanges()
        {
            if (_stationProfile == null)
                return true;

            DailyUniqueSaveModel currentUnique = BuildDailyUniqueSaveModel();
            List<DailyEventRowModel> currentEvents = BuildDailyEventsSaveModel();

            long dateRep = GetSelectedDateRep();

            if (HasStationDailyDataChanges(dateRep))
                return true;

            if (CommonRecordComparisonService.HasUniqueChanges(_loadedUniqueRow, currentUnique))
                return true;

            if (CommonRecordComparisonService.HasEventsChanges(_loadedEventsRows, currentEvents))
                return true;

            return false;
        }

        #endregion

        #region ساخت مدل‌های ذخیره‌سازی

        /// <summary>
        /// مقادیر بخش خلاصه روزانه را به مدل قابل ذخیره در tbl_unique تبدیل می‌کند
        /// تاریخ از کنترل تاریخ شمسی خوانده می‌شود و مقادیر عددی با تبدیل امن استخراج می‌شوند
        /// </summary>
        private DailyUniqueSaveModel BuildDailyUniqueSaveModel()
        {
            long dateRep = GetSelectedDateRep();

            return new DailyUniqueSaveModel
            {
                DateRep = dateRep,
                IrFuel = ConvertToDouble(txt_irFuel.Text),
                TurbineFuel = ConvertToDouble(txt_TurbineFuel.Text),
                TurbineFlow = ConvertToDouble(txt_Flow.Text),
                NonTurbineFlow = ConvertToDouble(txt_nonFlow.Text),
                Vent = ConvertToDouble(txt_Vent.Text)
            };
        }

        /// <summary>
        /// ردیف‌های dgvEvents را به مدل‌های قابل ذخیره در tbl_events تبدیل می‌کند
        /// فقط ردیف‌هایی که واحد، نوع رویداد و ساعت معتبر دارند وارد خروجی می‌شوند
        /// </summary>
        private List<DailyEventRowModel> BuildDailyEventsSaveModel()
        {
            long dateRep = GetSelectedDateRep();

            List<DailyEventRowModel> list = new();

            foreach (DataGridViewRow row in dgvEvents.Rows)
            {
                if (row.IsNewRow)
                    continue;

                string rawUnit = row.Cells[1].Value?.ToString()?.Trim() ?? "";
                string rawEventType = row.Cells[2].Value?.ToString()?.Trim() ?? "";
                string eventTime = row.Cells[3].Value?.ToString()?.Trim() ?? "";
                string rawRemark = row.Cells[4].Value?.ToString()?.Trim() ?? "";

                string dbUnit = UnitMapper.ToDatabase(rawUnit);
                string dbEventType = EventNormalizationService.NormalizeEventTypeForDatabase(rawEventType);

                string dbRemark = "";

                if (dbEventType == "NSD" || dbEventType == "ESD")
                {
                    dbRemark = rawRemark;

                    if (dbRemark.Length > 55)
                        dbRemark = dbRemark[..55];
                }

                if (string.IsNullOrWhiteSpace(dbUnit) ||
                    string.IsNullOrWhiteSpace(dbEventType) ||
                    string.IsNullOrWhiteSpace(eventTime))
                {
                    continue;
                }

                list.Add(new DailyEventRowModel
                {
                    DateRep = dateRep,
                    Unit = dbUnit,
                    EventType = dbEventType,
                    EventTime = eventTime,
                    Remark = dbRemark
                });
            }

            return list;
        }

        /// <summary>
        /// داده‌های فعلی dgvData را برای ایستگاه رشت استخراج می‌کند
        /// خروجی این متد برای ساخت مدل ذخیره‌سازی tbl_data رشت استفاده می‌شود
        /// </summary>
        public List<RashtRowDto> ExtractRashtGridData()
        {
            List<RashtRowDto> list = new();

            for (int r = 0; r < 12; r++)
            {
                DataGridViewRow row = dgvData.Rows[r];

                list.Add(new RashtRowDto
                {
                    TimeRep = row.Cells[0].Value?.ToString() ?? "",
                    InP = TryGetDouble(row.Cells[1].Value),
                    OutP = TryGetDouble(row.Cells[2].Value),
                    LineFP = TryGetDouble(row.Cells[3].Value),
                    Line40P = TryGetDouble(row.Cells[4].Value),
                    Line30P = TryGetDouble(row.Cells[5].Value),
                    U1St = row.Cells[6].Value?.ToString(),
                    U1Rpm = TryGetInt(row.Cells[7].Value),
                    U2St = row.Cells[8].Value?.ToString(),
                    U2Rpm = TryGetInt(row.Cells[9].Value),
                    U3St = row.Cells[10].Value?.ToString(),
                    U3Rpm = TryGetInt(row.Cells[11].Value),
                    Rec = TryGetDouble(row.Cells[12].Value),
                    Flow = TryGetDouble(row.Cells[13].Value),
                    InT = TryGetDouble(row.Cells[14].Value),
                    OutT = TryGetDouble(row.Cells[15].Value),
                    AmbT = TryGetDouble(row.Cells[16].Value),
                    Ratio = TryGetDouble(row.Cells[17].Value)
                });
            }

            return list;
        }

        /// <summary>
        /// داده‌های فعلی dgvData را برای ایستگاه رامسر استخراج می‌کند
        /// خروجی این متد برای ساخت مدل ذخیره‌سازی tbl_data رامسر استفاده می‌شود
        /// </summary>
        public List<RamsarRowDto> ExtractRamsarGridData()
        {
            List<RamsarRowDto> list = new();

            for (int r = 0; r < 12; r++)
            {
                DataGridViewRow row = dgvData.Rows[r];

                list.Add(new RamsarRowDto
                {
                    TimeRep = row.Cells[0].Value?.ToString() ?? "",
                    InP = TryGetDouble(row.Cells[1].Value),
                    OutP = TryGetDouble(row.Cells[2].Value),
                    U1St = row.Cells[3].Value?.ToString(),
                    U1Rpm = TryGetInt(row.Cells[4].Value),
                    U2St = row.Cells[5].Value?.ToString(),
                    U2Rpm = TryGetInt(row.Cells[6].Value),
                    U3St = row.Cells[7].Value?.ToString(),
                    U3Rpm = TryGetInt(row.Cells[8].Value),
                    U4St = row.Cells[9].Value?.ToString(),
                    U4Rpm = TryGetInt(row.Cells[10].Value),
                    Rec = TryGetDouble(row.Cells[11].Value),
                    Flow = TryGetDouble(row.Cells[12].Value),
                    InT = TryGetDouble(row.Cells[13].Value),
                    OutT = TryGetDouble(row.Cells[14].Value),
                    AmbT = TryGetDouble(row.Cells[15].Value),
                    Ratio = TryGetDouble(row.Cells[16].Value)
                });
            }

            return list;
        }

        #endregion

        #region عملیات رویدادها

        /// <summary>
        /// بخش رویدادها را قابل ویرایش یا غیرفعال می‌کند
        /// در حالت Loaded رویدادها فقط نمایش داده می‌شوند و در حالت‌های Empty، Pasted و Editing قابل تغییر هستند
        /// </summary>
        private void SetEventsGridEditable(bool editable)
        {
            dgvEvents.ReadOnly = true;
            dgvEvents.Enabled = editable;

            btnAdd.Enabled = editable;
            btnDeleteItem.Enabled = editable;

            cmbUnits.Enabled = editable;
            cmbType.Enabled = editable;
            dtpTime.Enabled = editable;

            if (!editable)
            {
                txtRemark.Enabled = false;
                btnEndSelection.Enabled = false;
                btnEndSelection.Visible = false;
            }
        }

        /// <summary>
        /// دکمه افزودن رویداد بسته به حالت فعلی، رویداد جدید اضافه می‌کند یا تغییرات سطر انتخاب‌شده را اعمال می‌کند
        /// </summary>
        private void btnAdd_Click(object sender, EventArgs e)
        {
            switch (_eventEntryMode)
            {
                case EventEntryMode.Add:
                    AddEventRow();
                    break;

                case EventEntryMode.Apply:
                    ApplyEventRowChanges();
                    break;
            }
        }

        /// <summary>
        /// یک رویداد جدید را از کنترل‌های ورودی به dgvEvents اضافه می‌کند
        /// مقدار Remark فقط برای NSD و ESD ذخیره می‌شود و برای سایر رویدادها خالی خواهد بود
        /// </summary>
        private void AddEventRow()
        {
            if (!CanModifyEvents())
            {
                UiMessageService.ShowWarning("افزودن رویداد در این حالت مجاز نیست", "هشدار");

                return;
            }

            string displayUnit = cmbUnits.Text.Trim();
            string unit = UnitMapper.ToDatabase(displayUnit);
            string eventType = cmbType.Text.Trim().ToUpper();
            string eventTime = dtpTime.Value.ToString("HH:mm");
            string remark = txtRemark.Text.Trim();

            if (eventType == "OH" && !ConfirmOverhaulEvent())
                return;


            if (string.IsNullOrWhiteSpace(unit) ||
                string.IsNullOrWhiteSpace(eventType))
            {
                UiMessageService.ShowWarning("لطفاً اطلاعات رویداد را کامل وارد کنید", "هشدار");
                return;
            }

            if (eventType != "NSD" && eventType != "ESD")
                remark = "";

            if (remark.Length > 55)
                remark = remark[..55];

            int rowIndex = dgvEvents.Rows.Add();
            DataGridViewRow row = dgvEvents.Rows[rowIndex];

            row.Cells[0].Value = rowIndex + 1;
            row.Cells[1].Value = unit;
            row.Cells[2].Value = eventType;
            row.Cells[3].Value = eventTime;
            row.Cells[4].Value = remark;

            dgvEvents.ClearSelection();
            row.Selected = true;

            if (dgvEvents.Columns.Count > 1)
                dgvEvents.CurrentCell = row.Cells[1];

            ClearEventEntryControls();
        }

        /// <summary>
        /// تغییرات کنترل‌های ورودی رویداد را روی سطر انتخاب‌شده dgvEvents اعمال می‌کند
        /// این متد فقط زمانی اجرا می‌شود که کاربر قبلاً یک سطر رویداد را انتخاب کرده باشد
        /// </summary>
        private void ApplyEventRowChanges()
        {
            if (!CanModifyEvents())
            {
                UiMessageService.ShowWarning("ویرایش رویداد در این حالت مجاز نیست", "هشدار");
                return;
            }

            if (dgvEvents.CurrentRow == null || dgvEvents.CurrentRow.IsNewRow)
            {
                UiMessageService.ShowWarning("ابتدا یک سطر معتبر را انتخاب کنید", "هشدار");
                return;
            }

            string displayUnit = cmbUnits.Text.Trim();
            string unit = UnitMapper.ToDatabase(displayUnit);
            string eventType = cmbType.Text.Trim().ToUpper();
            string eventTime = dtpTime.Value.ToString("HH:mm");
            string remark = txtRemark.Text.Trim();

            if (string.IsNullOrWhiteSpace(unit) ||
                string.IsNullOrWhiteSpace(eventType))
            {
                UiMessageService.ShowWarning("لطفاً اطلاعات رویداد را کامل وارد کنید", "هشدار");
                return;
            }

            if (eventType != "NSD" && eventType != "ESD")
                remark = "";

            if (remark.Length > 55)
                remark = remark[..55];

            DataGridViewRow row = dgvEvents.CurrentRow;

            string oldEventType = row.Cells[2].Value?.ToString()?.Trim().ToUpper() ?? "";

            if (oldEventType != "OH" && eventType == "OH" && !ConfirmOverhaulEvent())
                return;

            row.Cells[1].Value = unit;
            row.Cells[2].Value = eventType;
            row.Cells[3].Value = eventTime;
            row.Cells[4].Value = remark;

            dgvEvents.ClearSelection();
            ClearEventEntryControls();
        }

        /// <summary>
        /// با کلیک روی سطر رویداد، اطلاعات آن سطر را وارد کنترل‌های ورودی می‌کند
        /// پس از این کار فرم ورود رویداد وارد حالت Apply می‌شود تا کاربر بتواند همان سطر را اصلاح کند
        /// </summary>
        private void dgvEvents_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (_currentMode != RecordFormMode.Empty &&
                    _currentMode != RecordFormMode.Pasted &&
                    _currentMode != RecordFormMode.Editing)
                {
                    return;
                }

                if (e.RowIndex < 0 || e.RowIndex >= dgvEvents.Rows.Count)
                    return;

                DataGridViewRow row = dgvEvents.Rows[e.RowIndex];

                if (row.IsNewRow)
                    return;

                string rawUnit = row.Cells[1].Value?.ToString() ?? "";
                cmbUnits.Text = UnitMapper.ToDisplay(rawUnit);
                cmbType.Text = row.Cells[2].Value?.ToString()?.Trim() ?? "";

                string timeText = row.Cells[3].Value?.ToString()?.Trim() ?? "";

                dtpTime.Value = TimeSpan.TryParse(timeText, out TimeSpan ts)
                    ? DateTime.Today.Add(ts)
                    : DateTime.Today;

                txtRemark.Text = row.Cells[4].Value?.ToString()?.Trim() ?? "";

                _eventEntryMode = EventEntryMode.Apply;
                btnAdd.Text = "Apply";

                btnEndSelection.Enabled = true;
                btnEndSelection.Visible = true;

                UpdateRemarkState();
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در بارگذاری رویداد برای ویرایش", ex, "خطا");
            }
        }

        /// <summary>
        /// انتخاب فعلی رویداد را لغو می‌کند و فرم ورود رویداد را از حالت Apply به حالت Add برمی‌گرداند
        /// </summary>
        private void btnEndSelection_Click(object sender, EventArgs e)
        {
            try
            {
                dgvEvents.ClearSelection();
                ClearEventEntryControls();
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در پایان انتخاب رویداد", ex, "خطا");
            }
        }

        /// <summary>
        /// سطر انتخاب‌شده از dgvEvents را حذف می‌کند
        /// حذف فقط در حالت‌های قابل ویرایش مجاز است و پس از حذف، شماره نمایشی سطرها دوباره ساخته می‌شود
        /// </summary>
        private void btnDeleteItem_Click(object sender, EventArgs e)
        {
            if (!CanModifyEvents())
            {
                UiMessageService.ShowWarning("حذف رویداد فقط در حالت ثبت جدید، ویرایش یا Paste مجاز است", "هشدار");
                return;
            }

            if (dgvEvents.Rows.Count == 0)
            {
                UiMessageService.ShowInfo("هیچ سطری برای حذف وجود ندارد", "اطلاع");
                return;
            }

            if (dgvEvents.CurrentRow == null)
            {
                UiMessageService.ShowWarning("ابتدا یک سطر را برای حذف انتخاب کنید", "هشدار");
                return;
            }

            DataGridViewRow selectedRow = dgvEvents.CurrentRow;

            if (selectedRow.IsNewRow)
            {
                UiMessageService.ShowWarning("این سطر قابل حذف نیست، لطفاً یک سطر معتبر را انتخاب کنید", "هشدار");
                return;
            }

            string message = UiMessageService.Paragraphs(
                "سطر انتخاب‌شده از لیست رویدادها حذف خواهد شد.",
                "این عملیات قابل بازگشت نیست.",
                "آیا ادامه می‌دهید؟");

            if (!UiMessageService.ConfirmDanger(
                    message,
                    "تأیید حذف"))
            {
                return;
            }

            int removedIndex = selectedRow.Index;

            dgvEvents.Rows.Remove(selectedRow);

            RenumberEventGridRows();

            if (dgvEvents.Rows.Count > 0)
            {
                int targetRowIndex = Math.Min(removedIndex, dgvEvents.Rows.Count - 1);

                while (targetRowIndex > 0 && dgvEvents.Rows[targetRowIndex].IsNewRow)
                    targetRowIndex--;

                dgvEvents.ClearSelection();

                if (!dgvEvents.Rows[targetRowIndex].IsNewRow)
                {
                    dgvEvents.Rows[targetRowIndex].Selected = true;

                    dgvEvents.CurrentCell = dgvEvents.Columns.Contains("colUnit")
                        ? dgvEvents.Rows[targetRowIndex].Cells["colUnit"]
                        : dgvEvents.Rows[targetRowIndex].Cells[1];
                }
            }
            else
            {
                txtRemark.Clear();
                UpdateRemarkState();
            }
        }

        /// <summary>
        /// شماره نمایشی ردیف‌های dgvEvents را پس از افزودن یا حذف رویداد بازسازی می‌کند
        /// این شماره فقط برای نمایش کاربر است و نقش کلید دیتابیس ندارد
        /// </summary>
        private void RenumberEventGridRows()
        {
            int counter = 1;

            foreach (DataGridViewRow row in dgvEvents.Rows)
            {
                if (row.IsNewRow)
                    continue;

                row.Cells[0].Value = counter;
                counter++;
            }
        }

        /// <summary>
        /// فعال بودن txtRemark را بر اساس نوع رویداد کنترل می‌کند
        /// Remark فقط برای NSD و ESD قابل ثبت است و در سایر رویدادها پاک و غیرفعال می‌شود
        /// </summary>
        private void UpdateRemarkState()
        {
            string eventType = cmbType.Text.Trim().ToUpper();

            bool allowRemark = eventType == "NSD" || eventType == "ESD";

            txtRemark.Enabled = allowRemark;

            if (!allowRemark)
                txtRemark.Clear();
        }

        /// <summary>
        /// کنترل‌های ورود رویداد را پاک می‌کند و دکمه btnAdd را به حالت افزودن رویداد برمی‌گرداند
        /// این متد بعد از Add، Apply یا پایان انتخاب سطر استفاده می‌شود
        /// </summary>
        private void ClearEventEntryControls()
        {
            cmbUnits.SelectedIndex = -1;
            cmbType.SelectedIndex = -1;

            dtpTime.Value = DateTime.Today;

            txtRemark.Clear();
            txtRemark.Enabled = false;

            _eventEntryMode = EventEntryMode.Add;
            btnAdd.Text = "Add";

            btnEndSelection.Enabled = false;
            btnEndSelection.Visible = false;
        }

        #endregion

        #region بارگذاری داده‌ها

        /// <summary>
        /// داده‌های لودشده جدول tbl_data ایستگاه رشت را داخل dgvData قرار می‌دهد
        /// این متد فقط مسئول نمایش داده است و منطق Query یا Mapping در سرویس‌های جداگانه انجام می‌شود
        /// </summary>
        public void LoadRashtRowsIntoGrid(List<DailyDataRowModel> rows)
        {
            if (rows == null || rows.Count == 0)
                return;

            int maxRows = Math.Min(rows.Count, 12);

            for (int r = 0; r < maxRows; r++)
            {
                DailyDataRowModel row = rows[r];

                dgvData.Rows[r].Cells[0].Value = row.TimeRep;
                dgvData.Rows[r].Cells[1].Value = row.InP.ToString("F1");
                dgvData.Rows[r].Cells[2].Value = row.OutP.ToString("F1");
                dgvData.Rows[r].Cells[3].Value = row.LineFP.ToString("F1");
                dgvData.Rows[r].Cells[4].Value = row.Line40P.ToString("F1");
                dgvData.Rows[r].Cells[5].Value = row.Line30P.ToString("F1");
                dgvData.Rows[r].Cells[6].Value = row.U1St;
                dgvData.Rows[r].Cells[7].Value = row.U1Rpm;
                dgvData.Rows[r].Cells[8].Value = row.U2St;
                dgvData.Rows[r].Cells[9].Value = row.U2Rpm;
                dgvData.Rows[r].Cells[10].Value = row.U3St;
                dgvData.Rows[r].Cells[11].Value = row.U3Rpm;
                dgvData.Rows[r].Cells[12].Value = row.Rec.ToString("F1");
                dgvData.Rows[r].Cells[13].Value = row.Flow.ToString("F1");
                dgvData.Rows[r].Cells[14].Value = row.InT.ToString("F1");
                dgvData.Rows[r].Cells[15].Value = row.OutT.ToString("F1");
                dgvData.Rows[r].Cells[16].Value = row.AmbT.ToString("F1");
                dgvData.Rows[r].Cells[17].Value = row.Ratio.ToString("F2");
            }
        }

        /// <summary>
        /// داده‌های لودشده جدول tbl_data ایستگاه رامسر را داخل dgvData قرار می‌دهد
        /// این متد با ساختار متفاوت ستون‌های رامسر هماهنگ است و فقط مسئول نمایش داده در گرید است
        /// </summary>
        public void LoadRamsarRowsIntoGrid(List<RamsarDailyDataRowModel> rows)
        {
            if (rows == null || rows.Count == 0)
                return;

            int maxRows = Math.Min(rows.Count, 12);

            for (int r = 0; r < maxRows; r++)
            {
                RamsarDailyDataRowModel row = rows[r];

                dgvData.Rows[r].Cells[0].Value = row.TimeRep;
                dgvData.Rows[r].Cells[1].Value = row.InP.ToString("F1");
                dgvData.Rows[r].Cells[2].Value = row.OutP.ToString("F1");
                dgvData.Rows[r].Cells[3].Value = row.U1St;
                dgvData.Rows[r].Cells[4].Value = row.U1Rpm;
                dgvData.Rows[r].Cells[5].Value = row.U2St;
                dgvData.Rows[r].Cells[6].Value = row.U2Rpm;
                dgvData.Rows[r].Cells[7].Value = row.U3St;
                dgvData.Rows[r].Cells[8].Value = row.U3Rpm;
                dgvData.Rows[r].Cells[9].Value = row.U4St;
                dgvData.Rows[r].Cells[10].Value = row.U4Rpm;
                dgvData.Rows[r].Cells[11].Value = row.Rec.ToString("F1");
                dgvData.Rows[r].Cells[12].Value = row.Flow.ToString("F1");
                dgvData.Rows[r].Cells[13].Value = row.InT.ToString("F1");
                dgvData.Rows[r].Cells[14].Value = row.OutT.ToString("F1");
                dgvData.Rows[r].Cells[15].Value = row.AmbT.ToString("F1");
                dgvData.Rows[r].Cells[16].Value = row.Ratio.ToString("F2");
            }
        }

        /// <summary>
        /// داده‌های تاریخ انتخاب‌شده را از سه جدول tbl_data، tbl_unique و tbl_events بارگذاری می‌کند
        /// اگر هیچ داده‌ای برای تاریخ انتخابی وجود نداشته باشد، فرم به حالت Empty برمی‌گردد
        /// </summary>
        private void LoadCurrentDateData()
        {
            bool hasData = false;

            try
            {
                if (_stationProfile == null)
                    throw new InvalidOperationException("پروفایل ایستگاه مقداردهی نشده است");

                long dateRep = GetSelectedDateRep();

                if (!AppSettingsService.IsDateAllowedByDataStartDate(dateRep))
                {
                    UiMessageService.ShowWarning(
                        AppSettingsService.BuildDataStartDateViolationMessage(dateRep),
                        "تاریخ غیرمجاز");

                    return;
                }

                _isBulkUpdatingGrid = true;

                dgvData.SuspendLayout();
                dgvEvents.SuspendLayout();

                try
                {
                    ReinitializeFormByCurrentDatabase();

                    LoadStationDailyData(dateRep);
                    CaptureStationLoadedSnapshot(dateRep);

                    _loadedUniqueRow =
                        CommonRecordQueryService.LoadDailyUnique(dateRep);

                    _loadedEventsRows =
                        CommonRecordQueryService.LoadDailyEvents(dateRep);

                    LoadEventsIntoGrid(
                        _loadedEventsRows ?? new List<DailyEventRowModel>());

                    hasData =
                        HasAnyLoadedDailyData() ||
                        _loadedUniqueRow != null ||
                        (_loadedEventsRows != null && _loadedEventsRows.Count > 0);

                    if (hasData)
                    {
                        LoadUniqueDataIntoControls(_loadedUniqueRow);

                        RecalculateRatioColumn(_pasteProfile!);
                        FillOddHoursInFirstColumn(_gridProfile!);
                        CalculateAverageRow(_pasteProfile!);
                        CalculateFlows(_pasteProfile!);
                    }
                }
                finally
                {
                    dgvEvents.ResumeLayout();
                    dgvData.ResumeLayout();

                    _isBulkUpdatingGrid = false;
                }

                if (!hasData)
                {
                    UiMessageService.ShowInfo(
                        "برای تاریخ انتخاب‌شده هیچ داده‌ای ثبت نشده است",
                        "اطلاع");

                    ResetRecordForm(false);
                    SetFormMode(RecordFormMode.Empty);

                    return;
                }

                SetFormMode(RecordFormMode.Loaded);

                dgvData.ClearSelection();
                dgvEvents.ClearSelection();
                dgvEvents.CurrentCell = null;
            }
            catch (Exception ex)
            {
                _isBulkUpdatingGrid = false;

                UiMessageService.ShowError(
                    "خطا در بارگذاری اطلاعات",
                    ex,
                    "خطا");
            }
        }

        /// <summary>
        /// دکمه Load داده‌های تاریخ انتخاب‌شده را از دیتابیس بارگذاری می‌کند
        /// </summary>
        private void btnLoad_Click(object? sender, EventArgs e)
        {
            try
            {
                LoadCurrentDateData();
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در بارگذاری اطلاعات", ex, "خطا");
            }
        }

        /// <summary>
        /// داده‌های روزانه ایستگاه فعال را از tbl_data خوانده و داخل گرید اصلی نمایش می‌دهد
        /// انتخاب سرویس مناسب بر اساس نوع پروفایل ایستگاه انجام می‌شود
        /// </summary>
        private void LoadStationDailyData(long dateRep)
        {
            if (_stationProfile is RashtStationRecordProfile)
            {
                List<DailyDataRowModel> rows = RashtRecordSaveService.LoadDailyData(dateRep);
                LoadRashtRowsIntoGrid(rows);
                return;
            }

            if (_stationProfile is RamsarStationRecordProfile)
            {
                List<RamsarDailyDataRowModel> rows = RamsarRecordPersistenceService.LoadDailyData(dateRep);
                LoadRamsarRowsIntoGrid(rows);
                return;
            }

            throw new NotSupportedException("پروفایل ایستگاه پشتیبانی نمی‌شود");
        }

        /// <summary>
        /// از داده‌های لودشده tbl_data یک Snapshot مستقل تهیه می‌کند
        /// Snapshot برای تشخیص تغییرات کاربر در حالت ویرایش استفاده می‌شود
        /// </summary>
        private void CaptureStationLoadedSnapshot(long dateRep)
        {
            if (_stationProfile is RashtStationRecordProfile)
            {
                List<DailyDataRowModel> rows = RashtRecordSaveService.LoadDailyData(dateRep);

                List<DailyDataRowModel> snapshot = rows
                    .Select(x => new DailyDataRowModel
                    {
                        TimeRep = x.TimeRep,
                        InP = x.InP,
                        OutP = x.OutP,
                        LineFP = x.LineFP,
                        Line40P = x.Line40P,
                        Line30P = x.Line30P,
                        U1St = x.U1St,
                        U1Rpm = x.U1Rpm,
                        U2St = x.U2St,
                        U2Rpm = x.U2Rpm,
                        U3St = x.U3St,
                        U3Rpm = x.U3Rpm,
                        Rec = x.Rec,
                        Flow = x.Flow,
                        InT = x.InT,
                        OutT = x.OutT,
                        AmbT = x.AmbT,
                        Ratio = x.Ratio
                    })
                    .ToList();

                SetLoadedDailyDataSnapshot(snapshot);
                return;
            }

            if (_stationProfile is RamsarStationRecordProfile)
            {
                List<RamsarDailyDataRowModel> rows = RamsarRecordPersistenceService.LoadDailyData(dateRep);

                List<RamsarDailyDataRowModel> snapshot = rows
                    .Select(x => new RamsarDailyDataRowModel
                    {
                        TimeRep = x.TimeRep,
                        InP = x.InP,
                        OutP = x.OutP,
                        U1St = x.U1St,
                        U1Rpm = x.U1Rpm,
                        U2St = x.U2St,
                        U2Rpm = x.U2Rpm,
                        U3St = x.U3St,
                        U3Rpm = x.U3Rpm,
                        U4St = x.U4St,
                        U4Rpm = x.U4Rpm,
                        Rec = x.Rec,
                        Flow = x.Flow,
                        InT = x.InT,
                        OutT = x.OutT,
                        AmbT = x.AmbT,
                        Ratio = x.Ratio
                    })
                    .ToList();

                SetLoadedDailyDataSnapshot(snapshot);
                return;
            }

            throw new NotSupportedException("پروفایل ایستگاه پشتیبانی نمی‌شود");
        }

        /// <summary>
        /// داده‌های tbl_unique را داخل کنترل‌های خلاصه روزانه قرار می‌دهد
        /// اگر داده‌ای وجود نداشته باشد، کنترل‌های مربوطه پاک می‌شوند
        /// </summary>
        private void LoadUniqueDataIntoControls(DailyUniqueLoadModel? model)
        {
            if (model == null)
            {
                txt_irFuel.Clear();
                txt_TurbineFuel.Clear();
                txt_Flow.Clear();
                txt_nonFlow.Clear();
                txt_Vent.Clear();
                return;
            }

            txt_irFuel.Text = model.IrFuel.ToString("F1");
            txt_TurbineFuel.Text = model.TurbineFuel.ToString("F1");
            txt_Flow.Text = model.TurbineFlow.ToString("F1");
            txt_nonFlow.Text = model.NonTurbineFlow.ToString("F1");
            txt_Vent.Text = model.Vent.ToString("F1");
        }

        /// <summary>
        /// رویدادهای لودشده از tbl_events را داخل dgvEvents نمایش می‌دهد
        /// شماره ردیف‌ها فقط نمایشی است و کلید دیتابیس محسوب نمی‌شود
        /// </summary>
        private void LoadEventsIntoGrid(List<DailyEventRowModel> eventsRows)
        {
            dgvEvents.Rows.Clear();

            if (eventsRows == null || eventsRows.Count == 0)
                return;

            int counter = 1;

            foreach (DailyEventRowModel item in eventsRows)
            {
                int rowIndex = dgvEvents.Rows.Add();

                dgvEvents.Rows[rowIndex].Cells[0].Value = counter;
                dgvEvents.Rows[rowIndex].Cells[1].Value = UnitMapper.ToDatabase(item.Unit);
                dgvEvents.Rows[rowIndex].Cells[2].Value = item.EventType;
                dgvEvents.Rows[rowIndex].Cells[3].Value = item.EventTime;
                dgvEvents.Rows[rowIndex].Cells[4].Value = item.Remark;

                counter++;
            }
        }

        /// <summary>
        /// بررسی می‌کند Snapshot مربوط به tbl_data دارای داده لودشده است یا نه
        /// این متد بدون وابستگی به نوع واقعی Snapshot کار می‌کند
        /// </summary>
        private bool HasAnyLoadedDailyData()
        {
            object? snapshot = GetLoadedDailyDataSnapshot();

            if (snapshot is System.Collections.ICollection collection)
                return collection.Count > 0;

            return false;
        }

        /// <summary>
        /// Snapshot لودشده از tbl_data را ذخیره می‌کند
        /// این Snapshot بعداً برای تشخیص تغییرات داده‌های روزانه استفاده می‌شود
        /// </summary>
        public void SetLoadedDailyDataSnapshot(object? snapshot)
        {
            _loadedDailyDataSnapshot = snapshot;
        }

        /// <summary>
        /// Snapshot لودشده از tbl_data را برمی‌گرداند
        /// نوع واقعی خروجی به ایستگاه فعال وابسته است
        /// </summary>
        public object? GetLoadedDailyDataSnapshot()
        {
            return _loadedDailyDataSnapshot;
        }

        #endregion

        #region بررسی روزهای ناقص

        /// <summary>
        /// نام فارسی ماه شمسی را بر اساس شماره ماه برمی‌گرداند
        /// این متد برای ساخت پیام گزارش روزهای ناقص استفاده می‌شود
        /// </summary>
        private static string GetPersianMonthName(int month)
        {
            return month switch
            {
                1 => "فروردین",
                2 => "اردیبهشت",
                3 => "خرداد",
                4 => "تیر",
                5 => "مرداد",
                6 => "شهریور",
                7 => "مهر",
                8 => "آبان",
                9 => "آذر",
                10 => "دی",
                11 => "بهمن",
                12 => "اسفند",
                _ => "نامشخص"
            };
        }

        /// <summary>
        /// روزهای ناقص ماه انتخاب‌شده را بررسی می‌کند
        /// این بررسی بر اساس ماه تاریخ انتخاب‌شده در کنترل تاریخ شمسی انجام می‌شود
        /// </summary>
        private void btnMissing_Click(object sender, EventArgs e)
        {
            try
            {
                if (datePicker == null || string.IsNullOrWhiteSpace(datePicker.ShamsiDate))
                {
                    UiMessageService.ShowWarning("لطفاً تاریخ را از تقویم انتخاب کنید", "هشدار");
                    return;
                }

                string shamsiDate = datePicker.ShamsiDate.Trim();
                string[] parts = shamsiDate.Split('/');

                if (parts.Length != 3 ||
                    !int.TryParse(parts[0], out int year) ||
                    !int.TryParse(parts[1], out int month))
                {
                    UiMessageService.ShowWarning("فرمت تاریخ نامعتبر است", "هشدار");
                    return;
                }

                MissingDaysResultModel result = MissingDaysService.GetMissingDaysForMonth(year, month);

                string message = MissingDaysTextService.BuildMonthMessage(result, GetPersianMonthName);

                if (result.HasMissingDays)
                {
                    UiMessageService.ShowWarning(
                        message,
                        "روزهای ناقص");
                }
                else
                {
                    UiMessageService.ShowInfo(
                        message,
                        "روزهای ناقص");
                }
            }
            catch (Exception ex)
            {
                UiMessageService.ShowError("خطا در بررسی روزهای ناقص", ex, "خطا");
            }
        }

        #endregion

        #region ثبت داده‌های ایستگاه

        /// <summary>
        /// داده‌های فعلی dgvData را بر اساس ایستگاه فعال استخراج و در tbl_data همان ایستگاه ذخیره می‌کند
        /// این متد داخل Transaction اصلی ذخیره روزانه اجرا می‌شود
        /// </summary>
        private void InsertStationDailyData(SqliteConnection conn, SqliteTransaction tx, long dateRep)
        {
            if (_stationProfile is RashtStationRecordProfile)
            {
                List<RashtRowDto> rows = ExtractRashtGridData();

                DailyDataSaveModel model =
                    RashtRecordMapperService.BuildSaveModel(rows, dateRep);

                RashtRecordSaveService.InsertDailyDataOnly(conn, tx, model);
                return;
            }

            if (_stationProfile is RamsarStationRecordProfile)
            {
                List<RamsarRowDto> rows = ExtractRamsarGridData();

                RamsarDailyDataSaveModel model =
                    RamsarRecordMapperService.BuildSaveModel(rows, dateRep);

                RamsarRecordPersistenceService.InsertDailyDataOnly(conn, tx, model);
                return;
            }

            throw new NotSupportedException("پروفایل ایستگاه پشتیبانی نمی‌شود");
        }

        /// <summary>
        /// بررسی می‌کند داده‌های فعلی tbl_data نسبت به Snapshot لودشده تغییر کرده‌اند یا نه
        /// این بررسی بر اساس نوع ایستگاه فعال و سرویس Comparison همان ایستگاه انجام می‌شود
        /// </summary>
        private bool HasStationDailyDataChanges(long dateRep)
        {
            if (_stationProfile is RashtStationRecordProfile)
            {
                object? rawSnapshot = GetLoadedDailyDataSnapshot();

                if (rawSnapshot is not List<DailyDataRowModel> loadedRows)
                    return true;

                List<RashtRowDto> rows = ExtractRashtGridData();

                DailyDataSaveModel current =
                    RashtRecordMapperService.BuildSaveModel(rows, dateRep);

                return RashtRecordComparisonService.HasDailyDataChanges(loadedRows, current);
            }

            if (_stationProfile is RamsarStationRecordProfile)
            {
                object? rawSnapshot = GetLoadedDailyDataSnapshot();

                if (rawSnapshot is not List<RamsarDailyDataRowModel> loadedRows)
                    return true;

                List<RamsarRowDto> rows = ExtractRamsarGridData();

                RamsarDailyDataSaveModel current =
                    RamsarRecordMapperService.BuildSaveModel(rows, dateRep);

                return RamsarRecordComparisonService.HasDailyDataChanges(loadedRows, current);
            }

            throw new NotSupportedException("پروفایل ایستگاه فعال پشتیبانی نمی‌شود");
        }

        #endregion

        #region قفل ماه

        /// <summary>
        /// سال و ماه را از date_rep استخراج می‌کند
        /// </summary>
        private static void SplitYearMonth(long dateRep, out int year, out int month)
        {
            string value = dateRep.ToString();

            if (value.Length != 8)
                throw new InvalidOperationException("فرمت تاریخ انتخاب‌ شده معتبر نیست");

            year = int.Parse(value[..4]);
            month = int.Parse(value.Substring(4, 2));
        }

        /// <summary>
        /// بررسی می‌کند آیا ماه تاریخ انتخاب‌شده قفل شده است یا نه
        /// </summary>
        private bool IsSelectedMonthLocked(long dateRep)
        {
            SplitYearMonth(dateRep, out int year, out int month);

            if (!MonthlyLockService.IsMonthLocked(year, month))
                return false;

            UiMessageService.ShowWarning(MonthlyLockService.BuildLockedMonthMessage(year, month), "غیر مجاز");
            return true;
        }


        #endregion

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                string result = TestDataSeederService.CopyTemplateDayToFullMonth(
                    templateDateRep: 14050101,
                    targetYear: 1405,
                    targetMonth: 1);

                MessageBox.Show(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button11_Click(object sender, EventArgs e)
        {
            try
            {
                string result = TestDataSeederService.CopyTemplateDayToFullYear(14050101, 1405);

                MessageBox.Show(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
