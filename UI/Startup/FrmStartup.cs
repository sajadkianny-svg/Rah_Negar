using Rah_Negar.Core;
using Rah_Negar.Models;
using Rah_Negar.Services;
using Rah_Negar.UI.Controls;
using Rah_Negar.UI.Forms;
using Rah_Negar.UI.Forms.Base;
using Rah_Negar.Services.UI;
using Rah_Negar.Utils;
using Rah_Negar.Foundation.Application.Provisioning;

namespace Rah_Negar.UI.Startup
{
    public partial class FrmStartup : BaseForm
    {
        // ================= Fields =================

        /// <summary>
        /// نوع ایستگاه انتخاب‌شده توسط کاربر
        /// </summary>
        /// <summary>
        /// جلوگیری از اجرای ناخواسته رویدادها هنگام بارگذاری فرم
        /// </summary>

        /// <summary>
        /// وضعیت نمایش رمز عبور
        /// </summary>
        private bool _passwordVisible;

        private TextBox _stationNameInput = null!;
        private ComboBox _unitCountInput = null!;
        private CheckBox _linePressureInput = null!;
        private Label _profileReview = null!;
        private TabControl _wizardSteps = null!;
        private TextBox? _u5Run;
        private TextBox? _u5Oh;
        private ComboBox? _u5Status;



        // ================= Constructor / Load =================

        public FrmStartup()
        {
            InitializeComponent();
            ConfigureCanonicalWizard();
            CreateUnitFiveInputs();
            AcceptButton = btnSave;
            CancelButton = btnCancel;
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
                BindRuntimeEvents();
                BindNumericTextBoxes();
                ToggleEsdExtraRuntimeInput(chAddHoursAfterEsd.Checked);

            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "FrmStartup.Initialize");
                UiMessageService.ShowError("آماده‌سازی فرم راه‌اندازی انجام نشد. تنظیمات دسترسی داده را بررسی کنید.", "خطا");
            }
        }

        private void ConfigureCanonicalWizard()
        {
            grpStation.Visible = false;
            grpStation.Text = string.Empty;
            rbRasht.Visible = false;
            rbRamsar.Visible = false;
            rbOther.Visible = false;
            rbRasht.Text = string.Empty;
            rbRamsar.Text = string.Empty;
            rbOther.Text = string.Empty;
            txtCustom.Visible = false;
            lblCustom.Visible = false;

            groupBox1.Location = new Point(13, 150);
            grpSecurity.Location = new Point(262, 150);
            grpRuntime.Location = new Point(13, 220);
            btnCancel.Location = new Point(365, 410);
            btnSave.Location = new Point(481, 410);
            ClientSize = new Size(664, 445);

            _wizardSteps = new TabControl
            {
                Name = "wizardSteps",
                Location = new Point(13, 10),
                Size = new Size(638, 130),
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Font = UiStyleService.CreateFont()
            };

            TabPage identity = CreateWizardPage("stepIdentity", "۱. نام ایستگاه");
            _stationNameInput = new TextBox
            {
                Name = "txtStationName",
                Location = new Point(280, 25),
                Size = new Size(300, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                MaxLength = 120,
                RightToLeft = RightToLeft.Yes,
                TextAlign = HorizontalAlignment.Right
            };
            identity.Controls.Add(new Label { Text = "نام ایستگاه:", AutoSize = true, Location = new Point(500, 31), Font = UiStyleService.CreateFont() });
            identity.Controls.Add(_stationNameInput);

            TabPage units = CreateWizardPage("stepUnits", "۲. تعداد واحدها");
            _unitCountInput = new ComboBox
            {
                Name = "cmbUnitCount",
                Location = new Point(430, 25),
                Size = new Size(120, 27),
                DropDownStyle = ComboBoxStyle.DropDownList,
                RightToLeft = RightToLeft.Yes
            };
            _unitCountInput.Items.AddRange([3, 4, 5]);
            _unitCountInput.SelectedIndex = 0;
            units.Controls.Add(new Label { Text = "تعداد واحدها:", AutoSize = true, Location = new Point(555, 31), Font = UiStyleService.CreateFont() });
            units.Controls.Add(_unitCountInput);
            units.Controls.Add(new Label { Text = "فقط ۳، ۴ یا ۵ واحد پشتیبانی می‌شود.", AutoSize = true, Location = new Point(180, 31), Font = UiStyleService.CreateFont(8.5f) });

            TabPage parameters = CreateWizardPage("stepParameters", "۳. پارامترهای مجاز");
            _linePressureInput = new CheckBox
            {
                Name = "chkLinePressureParameters",
                Text = "پارامترهای فشار خط (FirstLine، 40in، 30in)",
                AutoSize = true,
                Location = new Point(300, 30),
                RightToLeft = RightToLeft.Yes
            };
            parameters.Controls.Add(_linePressureInput);
            parameters.Controls.Add(new Label { Text = "سایر پارامترهای اصلی طبق قرارداد محصول فعال هستند.", AutoSize = true, Location = new Point(35, 65), Font = UiStyleService.CreateFont(8.5f) });

            TabPage review = CreateWizardPage("stepReview", "۴. بازبینی تنظیمات");
            _profileReview = new Label
            {
                Name = "lblProfileReview",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                RightToLeft = RightToLeft.Yes,
                Padding = new Padding(20)
            };
            review.Controls.Add(_profileReview);

            TabPage launch = CreateWizardPage("stepLaunch", "۵. ثبت و راه‌اندازی");
            launch.Controls.Add(new Label
            {
                Text = "پس از تکمیل ورودی‌های پایه، روی «ثبت و راه‌اندازی» کلیک کنید.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                RightToLeft = RightToLeft.Yes
            });
            _wizardSteps.TabPages.AddRange([identity, units, parameters, review, launch]);
            Controls.Add(_wizardSteps);
            _wizardSteps.BringToFront();
            _stationNameInput.TextChanged += (_, _) => UpdateProfileReview();
            _unitCountInput.SelectedIndexChanged += (_, _) => UpdateProfileReview();
            _linePressureInput.CheckedChanged += (_, _) => UpdateProfileReview();
            UpdateProfileReview();
        }

        private static TabPage CreateWizardPage(string name, string text) => new()
        {
            Name = name,
            Text = text,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(10)
        };

        private void UpdateProfileReview()
        {
            if (_profileReview is null)
                return;
            string station = string.IsNullOrWhiteSpace(_stationNameInput.Text) ? "—" : _stationNameInput.Text.Trim();
            string units = _unitCountInput.SelectedItem?.ToString() ?? "—";
            string line = _linePressureInput.Checked ? "فعال" : "غیرفعال";
            _profileReview.Text = $"نام ایستگاه: {station}\r\nتعداد واحدها: {units}\r\nپارامترهای فشار خط: {line}";
        }

        private void CreateUnitFiveInputs()
        {
            _u5Run = CreateRuntimeTextBox("txtU5Run", 64, 169);
            _u5Oh = CreateRuntimeTextBox("txtU5OH", 160, 169);
            _u5Status = new ComboBox { Name = "cmbStU5", DropDownStyle = ComboBoxStyle.DropDownList, Items = { "ON", "OFF" }, Location = new Point(250, 169), Size = new Size(83, 23), RightToLeft = RightToLeft.Yes };
            Label label = new() { Name = "lblU5", Text = "واحد ۵", AutoSize = true, Location = new Point(19, 173), RightToLeft = RightToLeft.Yes };
            grpRuntime.Controls.AddRange([_u5Run, _u5Oh, _u5Status, label]);
            SetUnitRowVisible(5, false);
        }

        private static TextBox CreateRuntimeTextBox(string name, int x, int y) => new()
        {
            Name = name, Location = new Point(x, y), Size = new Size(90, 23), BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Center, RightToLeft = RightToLeft.No
        };

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
                _u5Run, _u5Oh,
                txtEsdExtraHours
            };

            foreach (TextBox? txt in numericBoxes)
            {
                if (txt != null)
                    txt.KeyPress += NumericTextBox_KeyPress;
            }
        }

        private int GetSelectedUnitCount() =>
            _unitCountInput.SelectedItem is int value ? value : 0;

        /// <summary>
        /// نمایش یا مخفی کردن ردیف‌های واحدها بر اساس تعداد واحد.
        /// </summary>
        private void ApplyRuntimeRowVisibility(int unitCount)
        {
            SetUnitRowVisible(3, unitCount >= 3);
            SetUnitRowVisible(4, unitCount >= 4);
            SetUnitRowVisible(5, unitCount >= 5);
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
                case 5:
                    if (_u5Run is not null) _u5Run.Visible = visible;
                    if (_u5Oh is not null) _u5Oh.Visible = visible;
                    if (_u5Status is not null) _u5Status.Visible = visible;
                    Control? label = grpRuntime.Controls["lblU5"];
                    if (label is not null) label.Visible = visible;
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
                5 => _u5Status ?? throw new InvalidOperationException("کنترل واحد ۵ آماده نیست"),
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

                UiMessageService.ShowMessageBox(
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
                ErrorLogger.Log(ex, "FrmStartup.InitializeApplication");
                UiMessageService.ShowError("راه‌اندازی اولیه انجام نشد. ورودی‌ها و دسترسی پوشه داده را بررسی کنید.", "خطا");
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
            if (string.IsNullOrWhiteSpace(txtPass.Text))
            {
                UiMessageService.ShowMessageBox(
                    "وارد کردن رمز عبور الزامی است",
                    "اعتبارسنجی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtPass.Focus();
                return false;
            }

            if (txtPass.Text != txtConfirm.Text)
            {
                UiMessageService.ShowMessageBox(
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

            int unitCount = GetSelectedUnitCount();

            if (string.IsNullOrWhiteSpace(_stationNameInput.Text))
            {
                UiMessageService.ShowMessageBox("نام ایستگاه را وارد کنید", "اعتبارسنجی", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _wizardSteps.SelectedIndex = 0;
                _stationNameInput.Focus();
                return false;
            }

            if (!TargetStationProfileRules.IsUnitCountSupported(unitCount))
            {
                UiMessageService.ShowMessageBox("تعداد واحدها فقط می‌تواند ۳، ۴ یا ۵ باشد", "اعتبارسنجی", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _wizardSteps.SelectedIndex = 1;
                return false;
            }

            for (int i = 1; i <= unitCount; i++)
            {
                if (!TryReadRuntimeValues(i, out _, out _))
                    return false;
            }

            long dataStartDate = GetDataStartDateRep();

            if (dataStartDate == 0)
            {
                UiMessageService.ShowMessageBox(
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
                    UiMessageService.ShowMessageBox(
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
            int unitCount = GetSelectedUnitCount();

            string stationName = _stationNameInput.Text.Trim();
            CanonicalProfileDefinition profileDefinition = CanonicalProfileDefinition.Create(
                stationName,
                unitCount,
                _linePressureInput.Checked
                    ? ["line_f_p", "line40_p", "line30_p"]
                    : Array.Empty<string>());

            double esdExtraHours = 0;

            if (chAddHoursAfterEsd.Checked)
                TryReadEsdExtraRuntimeHours(out esdExtraHours);

            StartupSetupData data = new()
            {
                StationType = StationType.Custom,
                StationName = stationName,
                ProfileDefinition = profileDefinition,
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
                UiMessageService.ShowMessageBox(
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

                case 5:
                    txtRun = _u5Run;
                    txtOH = _u5Oh;
                    break;
            }

            if (txtRun == null || txtOH == null)
            {
                UiMessageService.ShowMessageBox(
                    "کنترل‌های کارکرد واحدها به‌درستی تنظیم نشده‌اند",
                    "خطا",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return false;
            }

            if (!double.TryParse(txtRun.Text.Trim(), out runtime) || runtime < 0)
            {
                UiMessageService.ShowMessageBox(
                    $"مقدار کارکرد واحد {unitNo} معتبر نیست",
                    "اعتبارسنجی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtRun.Focus();
                return false;
            }

            if (!double.TryParse(txtOH.Text.Trim(), out afterOh) || afterOh < 0)
            {
                UiMessageService.ShowMessageBox(
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
