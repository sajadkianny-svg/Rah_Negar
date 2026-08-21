using Rah_Negar.Core;
using Rah_Negar.Models;
using Rah_Negar.Services;
using Rah_Negar.UI.Controls;
using Rah_Negar.UI.Forms;
using Rah_Negar.UI.Forms.Base;

namespace Rah_Negar.UI.Startup
{
    public partial class FrmStartup : BaseForm
    {
        // ================= Fields =================

        /// <summary>
        /// نوع ایستگاه انتخاب‌شده توسط کاربر
        /// </summary>
        private StationType _selectedStation = StationType.Unknown;

        /// <summary>
        /// جلوگیری از اجرای ناخواسته رویدادها هنگام بارگذاری فرم
        /// </summary>
        private bool _isFormReady;

        /// <summary>
        /// وضعیت نمایش رمز عبور
        /// </summary>
        private bool _passwordVisible;



        // ================= Constructor / Load =================

        public FrmStartup()
        {
            InitializeComponent();
            LoadMonths();
            LoadYears();

            InitializeStartupFormState();
            UpdateInitialBaseDateInfo();

            txtPass.UseSystemPasswordChar = true;
            txtConfirm.UseSystemPasswordChar = true;

            cmbDataStartYear.SelectedIndexChanged += (_, _) => UpdateInitialBaseDateInfo();
            cmbDataStartMonth.SelectedIndexChanged += (_, _) => UpdateInitialBaseDateInfo();

        }

        private void FrmStartup_Load(object sender, EventArgs e)
        {
            btnTogglePassword.Click += btnTogglePassword_Click;
            _passwordVisible = false;
            btnTogglePassword.Text = "👁";
        }

        // ================= Initialization =================

        /// <summary>
        /// مقداردهی اولیه فرم Startup
        /// </summary>
        private void InitializeStartupFormState()
        {
            try
            {
                BindStationEvents();
                BindRuntimeEvents();
                BindNumericTextBoxes();

                rbRasht.Checked = true;
                _isFormReady = true;

                ApplyStationProfileUi();
                ToggleEsdExtraRuntimeInput(chAddHoursAfterEsd.Checked);

            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در مقداردهی اولیه فرم راه‌اندازی" +
                    Environment.NewLine +
                    ex.Message,
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// تاریخ شروع مبنای داده‌ها را به صورت عددی برمی‌گرداند
        /// مثال: 14040501
        /// </summary>
        private long GetDataStartDateRep()
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
        /// مقداردهی سال‌های قابل انتخاب برای شروع ثبت داده‌ها
        /// </summary>
        private void LoadYears()
        {
            cmbDataStartYear.Items.Clear();

            int currentPersianYear = GetCurrentPersianYear();

            for (int year = currentPersianYear - 10; year <= currentPersianYear + 5; year++)
                cmbDataStartYear.Items.Add(year);

            cmbDataStartYear.SelectedIndex = -1;
        }

        private void LoadMonths()
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

            // مقدار پیش‌فرض (اختیاری)
            cmbDataStartMonth.SelectedIndex = -1;
        }

        /// اتصال رویدادهای انتخاب پروفایل ایستگاه
        /// </summary> 
        private void BindStationEvents()
        {
            rbRasht.CheckedChanged += StationRadio_CheckedChanged;
            rbRamsar.CheckedChanged += StationRadio_CheckedChanged;
            rbOther.CheckedChanged += StationRadio_CheckedChanged;
        }

        /// <summary>
        /// اتصال رویدادهای مرتبط با تنظیمات کارکرد
        /// </summary>
        private void BindRuntimeEvents()
        {
            chAddHoursAfterEsd.CheckedChanged += chkAddHoursAfterEsd_CheckedChanged;
        }

        /// <summary>
        /// اتصال کنترل عددی به TextBoxهای عددی فرم
        /// </summary>
        private void BindNumericTextBoxes()
        {
            TextBox?[] numericBoxes =
            {
                txtU1Run, txtU1OH,
                txtU2Run, txtU2OH,
                txtU3Run, txtU3OH,
                txtU4Run, txtU4OH,
                txtEsdExtraHours
            };

            foreach (TextBox? txt in numericBoxes)
            {
                if (txt != null)
                    txt.KeyPress += NumericTextBox_KeyPress;
            }
        }

        // ================= Station Profile UI =================

        /// <summary>
        /// رویداد تغییر انتخاب ایستگاه
        /// </summary>
        private void StationRadio_CheckedChanged(object? sender, EventArgs e)
        {
            if (!_isFormReady)
                return;

            try
            {
                ApplyStationProfileUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در تغییر پروفایل ایستگاه" +
                    Environment.NewLine +
                    ex.Message,
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// اعمال تغییرات رابط کاربری بر اساس ایستگاه انتخاب‌شده
        /// </summary>
        private void ApplyStationProfileUi()
        {
            _selectedStation = GetSelectedStationType();

            int unitCount = GetUnitCountByStation(_selectedStation);

            ApplyRuntimeRowVisibility(unitCount);
            ToggleCustomStationInput();

        }

        /// <summary>
        /// نمایش یا مخفی کردن فیلد نام ایستگاه سفارشی
        /// </summary>
        private void ToggleCustomStationInput()
        {
            bool isCustom = rbOther.Checked;

            lblCustom.Visible = isCustom;
            txtCustom.Visible = isCustom;

            if (isCustom)
            {
                BeginInvoke(new Action(() =>
                {
                    txtCustom.Focus();
                }));
            }
            else
            {
                txtCustom.Clear();
            }
        }

        /// <summary>
        /// تشخیص نوع ایستگاه بر اساس RadioButton انتخاب‌شده
        /// </summary>
        private StationType GetSelectedStationType()
        {
            if (rbRasht.Checked)
                return StationType.Rasht;

            if (rbRamsar.Checked)
                return StationType.Ramsar;

            if (rbOther.Checked)
                return StationType.Custom;

            return StationType.Unknown;
        }

        /// <summary>
        /// تعیین تعداد واحدها بر اساس نوع ایستگاه
        /// </summary>
        private static int GetUnitCountByStation(StationType stationType)
        {
            if (stationType == StationType.Custom)
                return 3;

            try
            {
                IStationProfile profile = ProfileManager.GetProfile(stationType);
                return profile.UnitCount;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// نمایش یا مخفی کردن ردیف‌های واحدها بر اساس تعداد واحد.
        /// </summary>
        private void ApplyRuntimeRowVisibility(int unitCount)
        {
            SetUnitRowVisible(3, unitCount >= 3);
            SetUnitRowVisible(4, unitCount >= 4);
        }

        /// <summary>
        /// نمایش یا مخفی کردن کنترل‌های مربوط به یک واحد.
        /// </summary>
        private void SetUnitRowVisible(int unitNo, bool visible)
        {
            switch (unitNo)
            {
                case 3:
                    lblU3.Visible = visible;
                    txtU3Run.Visible = visible;
                    txtU3OH.Visible = visible;
                    cmbStU3.Visible = visible;
                    break;

                case 4:
                    lblU4.Visible = visible;
                    txtU4Run.Visible = visible;
                    txtU4OH.Visible = visible;
                    cmbStU4.Visible = visible;
                    break;
            }
        }

        // ================= Runtime UI =================

        /// <summary>
        /// نوع رویداد اولیه واحد را از ComboBox مربوط به همان واحد می‌خواند
        /// </summary>
        private string GetInitialStatusByUnit(int unitNo)
        {
            ComboBox cmb = unitNo switch
            {
                1 => cmbStU1,
                2 => cmbStU2,
                3 => cmbStU3,
                4 => cmbStU4,
                _ => throw new ArgumentOutOfRangeException(nameof(unitNo))
            };

            string value = cmb.Text.Trim().ToUpperInvariant();

            return value switch
            {
                "ON" => "ON",
                "OFF" => "OFF",
                _ => string.Empty
            };
        }

        /// <summary>
        /// فعال یا غیرفعال کردن TextBox ساعت اضافه پس از ESD
        /// </summary>
        private void ToggleEsdExtraRuntimeInput(bool enabled)
        {
            txtEsdExtraHours.Enabled = enabled;

            if (!enabled)
            {
                txtEsdExtraHours.Text = "0";
                return;
            }

            if (string.IsNullOrWhiteSpace(txtEsdExtraHours.Text) ||
                txtEsdExtraHours.Text.Trim() == "0")
            {
                txtEsdExtraHours.Clear();
            }
        }

        private void chkAddHoursAfterEsd_CheckedChanged(object? sender, EventArgs e)
        {
            ToggleEsdExtraRuntimeInput(chAddHoursAfterEsd.Checked);
        }

        // ================= Password UI =================

        private void btnTogglePassword_Click(object? sender, EventArgs e)
        {
            _passwordVisible = !_passwordVisible;

            txtPass.UseSystemPasswordChar = !_passwordVisible;
            txtConfirm.UseSystemPasswordChar = !_passwordVisible;

            btnTogglePassword.Text = _passwordVisible ? "🙈" : "👁";
        }

        // ================= Buttons =================

        /// <summary>
        /// ذخیره تنظیمات اولیه و ساخت دیتابیس بر اساس پروفایل انتخابی
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidateStartupInputs())
                    return;

                StartupSetupData setupData = BuildStartupSetupData();

                StartupSetupService.InitializeApplication(setupData);

                MessageBox.Show(
                    "راه‌اندازی اولیه با موفقیت انجام شد",
                    "موفق",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                Hide();

                using FrmLogin login = new();
                login.ShowDialog();

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در انجام راه‌اندازی اولیه" +
                    Environment.NewLine +
                    ex.Message,
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// بستن فرم Startup
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        // ================= Validation =================

        /// <summary>
        /// اعتبارسنجی کامل ورودی‌های فرم Startup
        /// </summary>
        private bool ValidateStartupInputs()
        {
            if (_selectedStation == StationType.Unknown)
            {
                MessageBox.Show(
                    "لطفاً پروفایل ایستگاه را انتخاب کنید",
                    "اعتبارسنجی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            if (_selectedStation == StationType.Custom &&
                string.IsNullOrWhiteSpace(txtCustom.Text))
            {
                MessageBox.Show(
                    "لطفاً نام ایستگاه سفارشی را وارد کنید",
                    "اعتبارسنجی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtCustom.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtPass.Text))
            {
                MessageBox.Show(
                    "وارد کردن رمز عبور الزامی است",
                    "اعتبارسنجی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtPass.Focus();
                return false;
            }

            if (txtPass.Text != txtConfirm.Text)
            {
                MessageBox.Show(
                    "رمز عبور و تکرار آن با هم یکسان نیستند",
                    "اعتبارسنجی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtConfirm.Focus();
                return false;
            }

            if (chAddHoursAfterEsd.Checked)
            {
                if (!TryReadEsdExtraRuntimeHours(out _))
                    return false;
            }

            int unitCount = GetUnitCountByStation(_selectedStation);

            for (int i = 1; i <= unitCount; i++)
            {
                if (!TryReadRuntimeValues(i, out _, out _))
                    return false;
            }

            long dataStartDate = GetDataStartDateRep();

            if (dataStartDate == 0)
            {
                MessageBox.Show(
                    "سال و ماه مبنای شروع داده‌ها را انتخاب کنید",
                    "اعتبارسنجی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            for (int i = 1; i <= unitCount; i++)
            {
                string initialStatus = GetInitialStatusByUnit(i);

                if (string.IsNullOrWhiteSpace(initialStatus))
                {
                    MessageBox.Show(
                        $"وضعیت اولیه واحد {i} را انتخاب کنید",
                        "اعتبارسنجی",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// کنترل ورود فقط عدد و نقطه اعشار در TextBoxهای عددی
        /// </summary>
        private void NumericTextBox_KeyPress(object? sender, KeyPressEventArgs e)
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

        /// <summary>
        /// سال شمسی جاری سیستم را برمی‌گرداند
        /// </summary>
        private static int GetCurrentPersianYear()
        {
            System.Globalization.PersianCalendar calendar = new();
            return calendar.GetYear(DateTime.Now);
        }

        /// <summary>
        /// متن راهنمای تاریخ شروع مبنای داده‌ها را بر اساس سال و ماه انتخاب‌شده بروزرسانی می‌کند
        /// </summary>

        private void UpdateInitialBaseDateInfo()
        {
            long dateRep = GetDataStartDateRep();

            if (dateRep == 0)
            {
                lblInitialBaseDateInfo.Visible = false;
                lblDate.Visible = false;
                return;
            }

            lblDate.Text = FormatDateRep(dateRep);
            lblInitialBaseDateInfo.Visible = true;
            lblDate.Visible = true;
        }

        /// <summary>
        /// تاریخ عددی را به فرمت نمایشی yyyy/MM/dd تبدیل می‌کند
        /// </summary>
        private static string FormatDateRep(long dateRep)
        {
            string value = dateRep.ToString();

            if (value.Length != 8)
                return "-";

            string formatted =
                $"{value[..4]}/{value.Substring(4, 2)}/{value.Substring(6, 2)}";

            return ToPersianDigits(formatted);
        }

        private static string ToPersianDigits(string value)
        {
            return value
                .Replace('0', '۰')
                .Replace('1', '۱')
                .Replace('2', '۲')
                .Replace('3', '۳')
                .Replace('4', '۴')
                .Replace('5', '۵')
                .Replace('6', '۶')
                .Replace('7', '۷')
                .Replace('8', '۸')
                .Replace('9', '۹');
        }

        // ================= Data Building =================

        /// <summary>
        /// جمع‌آوری اطلاعات فرم و تبدیل آن به مدل StartupSetupData
        /// </summary>
        private StartupSetupData BuildStartupSetupData()
        {
            int unitCount = GetUnitCountByStation(_selectedStation);

            string stationName = _selectedStation switch
            {
                StationType.Rasht => "Rasht Station",
                StationType.Ramsar => "Ramsar Station",
                StationType.Custom => txtCustom.Text.Trim(),
                _ => string.Empty
            };

            double esdExtraHours = 0;

            if (chAddHoursAfterEsd.Checked)
                TryReadEsdExtraRuntimeHours(out esdExtraHours);

            StartupSetupData data = new()
            {
                StationType = _selectedStation,
                StationName = stationName,
                ResetPassword = txtPass.Text.Trim(),
                EsdExtraRuntimeEnabled = chAddHoursAfterEsd.Checked,
                EsdExtraRuntimeHours = esdExtraHours,
                ThemeIndex = 6,
                DataStartDateRep = GetDataStartDateRep()
            };

            for (int i = 1; i <= unitCount; i++)
            {
                double run = 0;
                double afterOh = 0;

                TryReadRuntimeValues(i, out run, out afterOh);

                string initialStatus = GetInitialStatusByUnit(i);

                data.UnitRuntimeBases.Add(new UnitRuntimeBase
                {
                    UnitNo = i,
                    BaseRuntimeHours = run,
                    BaseRuntimeAfterOHHours = afterOh,
                    InitialStatus = initialStatus,
                    InitialIsRunning = initialStatus == "ON"
                });
            }

            return data;
        }

        /// <summary>
        /// خواندن و تبدیل مقدار ساعت اضافه پس از ESD
        /// </summary>
        private bool TryReadEsdExtraRuntimeHours(out double hours)
        {
            hours = 0;

            if (!double.TryParse(txtEsdExtraHours.Text.Trim(), out hours) || hours < 0)
            {
                MessageBox.Show(
                    "مقدار ساعت اضافه شده معتبر نیست",
                    "اعتبارسنجی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtEsdExtraHours.Focus();
                return false;
            }

            return true;
        }

        /// <summary>
        /// خواندن و تبدیل مقادیر Runtime و After OH برای هر واحد
        /// </summary>
        private bool TryReadRuntimeValues(int unitNo, out double runtime, out double afterOh)
        {
            runtime = 0;
            afterOh = 0;

            TextBox? txtRun = null;
            TextBox? txtOH = null;

            switch (unitNo)
            {
                case 1:
                    txtRun = txtU1Run;
                    txtOH = txtU1OH;
                    break;

                case 2:
                    txtRun = txtU2Run;
                    txtOH = txtU2OH;
                    break;

                case 3:
                    txtRun = txtU3Run;
                    txtOH = txtU3OH;
                    break;

                case 4:
                    txtRun = txtU4Run;
                    txtOH = txtU4OH;
                    break;
            }

            if (txtRun == null || txtOH == null)
            {
                MessageBox.Show(
                    "کنترل‌های کارکرد واحدها به‌درستی تنظیم نشده‌اند",
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return false;
            }

            if (!double.TryParse(txtRun.Text.Trim(), out runtime) || runtime < 0)
            {
                MessageBox.Show(
                    $"مقدار کارکرد واحد {unitNo} معتبر نیست",
                    "اعتبارسنجی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtRun.Focus();
                return false;
            }

            if (!double.TryParse(txtOH.Text.Trim(), out afterOh) || afterOh < 0)
            {
                MessageBox.Show(
                    $"مقدار کارکرد بعد از اورهال واحد {unitNo} معتبر نیست",
                    "اعتبارسنجی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtOH.Focus();
                return false;
            }

            return true;
        }
    }
}