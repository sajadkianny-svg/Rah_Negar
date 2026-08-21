using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace ShamsiDatePickerLibrary
{
    /// <summary>
    /// کنترل انتخاب تاریخ شمسی.
    /// نسخه DPI-safe، بدون TextBox و Button داخلی.
    /// مناسب برای Scaleهای 100، 125، 150 و 200 درصد.
    /// </summary>
    public sealed class ShamsiDatePicker : UserControl
    {
        private readonly PersianCalendar persianCalendar = new();

        private string shamsiDate = string.Empty;
        private CalendarPopup? calendarPopup;

        private Rectangle previousRect;
        private Rectangle dateRect;
        private Rectangle calendarRect;
        private Rectangle nextRect;

        private HoverPart hoverPart = HoverPart.None;
        private HoverPart pressedPart = HoverPart.None;

        private Color pickerBackColor = Color.White;
        private Color pickerForeColor = Color.FromArgb(35, 35, 35);
        private Color pickerButtonBackColor = Color.WhiteSmoke;
        private Color pickerButtonForeColor = Color.FromArgb(35, 35, 35);
        private Color pickerAccentColor = Color.FromArgb(210, 220, 235);
        private Color pickerHoverColor = Color.FromArgb(235, 240, 248);

        public event EventHandler? ShamsiDateChanged;
        public event EventHandler? EnterPressed;

        private enum HoverPart
        {
            None,
            Previous,
            Date,
            Calendar,
            Next
        }

        public ShamsiDatePicker()
        {
            AutoSize = false;
            Size = new Size(220, 34);
            MinimumSize = new Size(180, 30);
            MaximumSize = Size.Empty;

            BackColor = pickerBackColor;
            TabStop = true;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            InitializeToday();
        }

        public string ShamsiDate
        {
            get => shamsiDate;
            set => SetShamsiDate(value, raiseEvent: true);
        }

        public void SetDisplaySize(int width, int height)
        {
            AutoSize = false;
            MaximumSize = Size.Empty;
            Size = new Size(width, height);
            MinimumSize = new Size(Math.Min(width, 180), Math.Min(height, 30));
            Invalidate();
        }

        public void ApplyTheme(
            Color backColor,
            Color foreColor,
            Color buttonBackColor,
            Color buttonForeColor,
            Color accentColor,
            Color hoverColor)
        {
            pickerBackColor = backColor;
            pickerForeColor = foreColor;
            pickerButtonBackColor = buttonBackColor;
            pickerButtonForeColor = buttonForeColor;
            pickerAccentColor = accentColor;
            pickerHoverColor = hoverColor;

            BackColor = pickerBackColor;

            calendarPopup = null;
            Invalidate();
        }

        public void MoveDateByDays(int days)
        {
            MoveSelectedDateByDays(days);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            CalculateLayout();

            using SolidBrush backBrush = new(pickerBackColor);
            e.Graphics.FillRectangle(backBrush, ClientRectangle);

            DrawButton(e.Graphics, previousRect, "‹", HoverPart.Previous);
            DrawDateArea(e.Graphics);
            DrawButton(e.Graphics, calendarRect, "▼", HoverPart.Calendar);
            DrawButton(e.Graphics, nextRect, "›", HoverPart.Next);

            using Pen borderPen = new(Color.FromArgb(180, 180, 180));
            e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }

        private void CalculateLayout()
        {
            int h = ClientSize.Height;
            int w = ClientSize.Width;

            if (h <= 0 || w <= 0)
                return;

            int buttonWidth = Math.Max(28, (int)Math.Round(h * 0.82));

            previousRect = new Rectangle(0, 0, buttonWidth, h);
            nextRect = new Rectangle(w - buttonWidth, 0, buttonWidth, h);
            calendarRect = new Rectangle(nextRect.Left - buttonWidth, 0, buttonWidth, h);

            dateRect = new Rectangle(
                previousRect.Right,
                0,
                Math.Max(60, calendarRect.Left - previousRect.Right),
                h);
        }

        private void DrawButton(Graphics g, Rectangle rect, string glyph, HoverPart part)
        {
            Color back =
                pressedPart == part ? pickerAccentColor :
                hoverPart == part ? pickerHoverColor :
                pickerButtonBackColor;

            using SolidBrush backBrush = new(back);
            g.FillRectangle(backBrush, rect);

            using Pen linePen = new(Color.FromArgb(210, 210, 210));
            g.DrawRectangle(linePen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);

            float fontSize = glyph == "▼"
                ? rect.Height * 0.34f
                : rect.Height * 0.55f;

            using Font font = new(
                "Segoe UI Symbol",
                fontSize,
                FontStyle.Regular,
                GraphicsUnit.Point);

            TextFormatFlags flags =
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine;

            Size textSize = TextRenderer.MeasureText(
                glyph,
                font,
                Size.Empty,
                flags);

            int x = rect.Left + (rect.Width - textSize.Width) / 2;
            int y = rect.Top + (rect.Height - textSize.Height) / 2;

            // اصلاح بصری برای glyphها
            if (glyph == "‹" || glyph == "›")
                y -= 2;

            if (glyph == "▼")
                y -= 1;

            TextRenderer.DrawText(
                g,
                glyph,
                font,
                new Point(x, y),
                pickerButtonForeColor,
                flags);
        }

        private void DrawDateArea(Graphics g)
        {
            Color back =
                pressedPart == HoverPart.Date ? pickerAccentColor :
                hoverPart == HoverPart.Date ? pickerHoverColor :
                pickerBackColor;

            using SolidBrush backBrush = new(back);
            g.FillRectangle(backBrush, dateRect);

            using Pen linePen = new(Color.FromArgb(220, 220, 220));
            g.DrawRectangle(linePen, dateRect.X, dateRect.Y, dateRect.Width - 1, dateRect.Height - 1);

            using Font dateFont = new(
                "Segoe UI",
                dateRect.Height * 0.32f,
                FontStyle.Regular,
                GraphicsUnit.Point);

            TextFormatFlags flags =
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine;

            Size textSize = TextRenderer.MeasureText(
                shamsiDate,
                dateFont,
                Size.Empty,
                flags);

            int x = dateRect.Left + (dateRect.Width - textSize.Width) / 2;
            int y = dateRect.Top + (dateRect.Height - textSize.Height) / 2 - 1;

            TextRenderer.DrawText(
                g,
                shamsiDate,
                dateFont,
                new Point(x, y),
                pickerForeColor,
                flags);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            HoverPart newPart = HitTest(e.Location);

            if (newPart != hoverPart)
            {
                hoverPart = newPart;
                Invalidate();
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverPart = HoverPart.None;
            pressedPart = HoverPart.None;
            Invalidate();

            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            pressedPart = HitTest(e.Location);
            Invalidate();

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            HoverPart clickedPart = HitTest(e.Location);
            HoverPart oldPressed = pressedPart;

            pressedPart = HoverPart.None;
            Invalidate();

            if (clickedPart != oldPressed)
            {
                base.OnMouseUp(e);
                return;
            }

            switch (clickedPart)
            {
                case HoverPart.Previous:
                    MoveSelectedDateByDays(-1);
                    break;

                case HoverPart.Next:
                    MoveSelectedDateByDays(1);
                    break;

                case HoverPart.Date:
                case HoverPart.Calendar:
                    OpenCalendarPopup();
                    break;
            }

            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left)
            {
                MoveSelectedDateByDays(-1);
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Right)
            {
                MoveSelectedDateByDays(1);
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                EnterPressed?.Invoke(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Down)
            {
                OpenCalendarPopup();
                e.SuppressKeyPress = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private HoverPart HitTest(Point point)
        {
            if (previousRect.Contains(point))
                return HoverPart.Previous;

            if (nextRect.Contains(point))
                return HoverPart.Next;

            if (calendarRect.Contains(point))
                return HoverPart.Calendar;

            if (dateRect.Contains(point))
                return HoverPart.Date;

            return HoverPart.None;
        }

        private void OpenCalendarPopup()
        {
            float scale = Math.Clamp(DeviceDpi / 96f, 1f, 1.5f);

            calendarPopup ??= new CalendarPopup(
                shamsiDate,
                pickerBackColor,
                pickerForeColor,
                pickerButtonBackColor,
                pickerButtonForeColor,
                pickerAccentColor,
                pickerHoverColor,
                scale);

            calendarPopup.SetCurrentDate(shamsiDate);

            Point screenPoint = PointToScreen(new Point(0, Height + 2));

            calendarPopup.StartPosition = FormStartPosition.Manual;
            calendarPopup.Location = screenPoint;

            if (calendarPopup.ShowDialog(FindForm()) == DialogResult.OK)
                SetShamsiDate(calendarPopup.SelectedDate, raiseEvent: true);
        }

        private void InitializeToday()
        {
            DateTime now = DateTime.Now;

            int year = persianCalendar.GetYear(now);
            int month = persianCalendar.GetMonth(now);
            int day = persianCalendar.GetDayOfMonth(now);

            SetShamsiDate($"{year:0000}/{month:00}/{day:00}", raiseEvent: false);
        }

        private void MoveSelectedDateByDays(int days)
        {
            if (!TryParseShamsiDate(shamsiDate, out DateTime currentDate))
                currentDate = DateTime.Now;

            DateTime newDate = currentDate.AddDays(days);

            int year = persianCalendar.GetYear(newDate);
            int month = persianCalendar.GetMonth(newDate);
            int day = persianCalendar.GetDayOfMonth(newDate);

            SetShamsiDate($"{year:0000}/{month:00}/{day:00}", raiseEvent: true);
        }

        private bool TryParseShamsiDate(string? value, out DateTime date)
        {
            date = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string[] parts = value.Split('/');

            if (parts.Length != 3)
                return false;

            if (!int.TryParse(parts[0], out int year) ||
                !int.TryParse(parts[1], out int month) ||
                !int.TryParse(parts[2], out int day))
            {
                return false;
            }

            try
            {
                date = persianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SetShamsiDate(string? value, bool raiseEvent)
        {
            string normalized = NormalizeDateText(value);

            if (string.IsNullOrWhiteSpace(normalized))
                return;

            if (string.Equals(shamsiDate, normalized, StringComparison.Ordinal))
                return;

            shamsiDate = normalized;
            Invalidate();

            if (raiseEvent)
                ShamsiDateChanged?.Invoke(this, EventArgs.Empty);
        }

        private static string NormalizeDateText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string[] parts = value.Trim().Split('/');

            if (parts.Length != 3)
                return value.Trim();

            if (!int.TryParse(parts[0], out int year))
                return value.Trim();

            if (!int.TryParse(parts[1], out int month))
                return value.Trim();

            if (!int.TryParse(parts[2], out int day))
                return value.Trim();

            return $"{year:0000}/{month:00}/{day:00}";
        }

        private sealed class CalendarPopup : Form
        {
            private readonly PersianCalendar calendar = new();
            private readonly Button[] dayButtons = new Button[42];

            private readonly Label lblYear;
            private readonly Label lblMonth;
            private readonly TableLayoutPanel grid;

            private readonly float scale;

            private readonly Color backColor;
            private readonly Color foreColor;
            private readonly Color buttonBackColor;
            private readonly Color buttonForeColor;
            private readonly Color accentColor;
            private readonly Color hoverColor;

            private int currentYear;
            private int currentMonth;
            private int selectedDay;

            private static readonly string[] MonthNames =
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
            };

            public string SelectedDate { get; private set; } = string.Empty;

            public CalendarPopup(
                string currentDate,
                Color backColor,
                Color foreColor,
                Color buttonBackColor,
                Color buttonForeColor,
                Color accentColor,
                Color hoverColor,
                float scale)
            {
                DoubleBuffered = true;

                this.scale = Math.Clamp(scale, 1f, 1.5f);

                this.backColor = backColor;
                this.foreColor = foreColor;
                this.buttonBackColor = buttonBackColor;
                this.buttonForeColor = buttonForeColor;
                this.accentColor = accentColor;
                this.hoverColor = hoverColor;

                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                Size = new Size(S(320), S(335));
                MinimumSize = Size;
                MaximumSize = Size;

                BackColor = backColor;
                RightToLeft = RightToLeft.Yes;
                Font = new Font("Tahoma", 9F, FontStyle.Regular);
                KeyPreview = true;

                ParseInitialDate(currentDate);

                Panel mainPanel = new()
                {
                    Dock = DockStyle.Fill,
                    BackColor = backColor,
                    Padding = new Padding(S(8))
                };

                Controls.Add(mainPanel);

                Panel headerPanel = new()
                {
                    Location = new Point(S(8), S(8)),
                    Size = new Size(S(304), S(38)),
                    BackColor = hoverColor
                };

                Button btnPrevYear = CreateNavButton("››", new Point(S(8), S(5)));
                Button btnNextYear = CreateNavButton("‹‹", new Point(S(262), S(5)));

                lblYear = new Label
                {
                    Location = new Point(S(48), S(5)),
                    Size = new Size(S(208), S(28)),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    ForeColor = foreColor,
                    Font = new Font("Tahoma", 9F, FontStyle.Bold)
                };

                headerPanel.Controls.Add(btnPrevYear);
                headerPanel.Controls.Add(lblYear);
                headerPanel.Controls.Add(btnNextYear);

                Panel monthPanel = new()
                {
                    Location = new Point(S(8), S(52)),
                    Size = new Size(S(304), S(36)),
                    BackColor = backColor
                };

                Button btnPrevMonth = CreateNavButton("›", new Point(S(8), S(4)));
                Button btnNextMonth = CreateNavButton("‹", new Point(S(262), S(4)));

                lblMonth = new Label
                {
                    Location = new Point(S(48), S(4)),
                    Size = new Size(S(208), S(28)),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    ForeColor = foreColor,
                    Font = new Font("Tahoma", 9F, FontStyle.Regular)
                };

                monthPanel.Controls.Add(btnPrevMonth);
                monthPanel.Controls.Add(lblMonth);
                monthPanel.Controls.Add(btnNextMonth);

                grid = new TableLayoutPanel
                {
                    Location = new Point(S(8), S(94)),
                    Size = new Size(S(304), S(190)),
                    RowCount = 7,
                    ColumnCount = 7,
                    BackColor = backColor,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };

                BuildGrid();

                Button btnToday = CreateActionButton("امروز", new Point(S(8), S(294)));
                Button btnCancel = CreateActionButton("انصراف", new Point(S(112), S(294)));
                Button btnOk = CreateActionButton("تأیید", new Point(S(216), S(294)));

                btnPrevMonth.Click += (_, _) => MoveMonth(-1);
                btnNextMonth.Click += (_, _) => MoveMonth(1);
                btnPrevYear.Click += (_, _) => MoveYear(-1);
                btnNextYear.Click += (_, _) => MoveYear(1);

                btnToday.Click += (_, _) => SelectToday();
                btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
                btnOk.Click += (_, _) => ConfirmSelectedDate();

                mainPanel.Controls.Add(headerPanel);
                mainPanel.Controls.Add(monthPanel);
                mainPanel.Controls.Add(grid);
                mainPanel.Controls.Add(btnToday);
                mainPanel.Controls.Add(btnCancel);
                mainPanel.Controls.Add(btnOk);

                KeyDown += CalendarPopup_KeyDown;

                UpdateCalendar();
            }

            private int S(int value)
            {
                return (int)Math.Round(value * scale);
            }

            public void SetCurrentDate(string currentDate)
            {
                ParseInitialDate(currentDate);
                UpdateCalendar();
            }

            private void ParseInitialDate(string currentDate)
            {
                string[] parts = currentDate.Split('/');

                if (parts.Length == 3 &&
                    int.TryParse(parts[0], out int year) &&
                    int.TryParse(parts[1], out int month) &&
                    int.TryParse(parts[2], out int day))
                {
                    currentYear = year;
                    currentMonth = month;
                    selectedDay = day;
                    return;
                }

                DateTime now = DateTime.Now;

                currentYear = calendar.GetYear(now);
                currentMonth = calendar.GetMonth(now);
                selectedDay = calendar.GetDayOfMonth(now);
            }

            private void BuildGrid()
            {
                grid.SuspendLayout();

                grid.RowStyles.Clear();
                grid.ColumnStyles.Clear();
                grid.Controls.Clear();

                for (int i = 0; i < 7; i++)
                {
                    grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / 7));
                    grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 7));
                }

                string[] weekDays = { "ش", "ی", "د", "س", "چ", "پ", "ج" };

                for (int col = 0; col < 7; col++)
                {
                    Label lbl = new()
                    {
                        Text = weekDays[col],
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        BackColor = backColor,
                        ForeColor = col == 6 ? Color.Firebrick : foreColor,
                        Font = new Font("Tahoma", 9F, FontStyle.Regular),
                        Margin = Padding.Empty
                    };

                    grid.Controls.Add(lbl, col, 0);
                }

                int index = 0;

                for (int row = 1; row < 7; row++)
                {
                    for (int col = 0; col < 7; col++)
                    {
                        Button dayButton = new()
                        {
                            Dock = DockStyle.Fill,
                            FlatStyle = FlatStyle.Flat,
                            BackColor = backColor,
                            ForeColor = foreColor,
                            Font = new Font("Tahoma", 9F, FontStyle.Regular),
                            Margin = new Padding(1),
                            Cursor = Cursors.Hand,
                            TabStop = false,
                            TextAlign = ContentAlignment.MiddleCenter
                        };

                        dayButton.FlatAppearance.BorderSize = 0;
                        dayButton.FlatAppearance.MouseOverBackColor = hoverColor;
                        dayButton.FlatAppearance.MouseDownBackColor = accentColor;

                        dayButton.Click += DayButton_Click;

                        dayButtons[index] = dayButton;
                        grid.Controls.Add(dayButton, col, row);

                        index++;
                    }
                }

                grid.ResumeLayout(false);
            }

            private void UpdateCalendar()
            {
                grid.SuspendLayout();

                lblYear.Text = currentYear.ToString("0000");
                lblMonth.Text = MonthNames[currentMonth - 1];

                foreach (Button button in dayButtons)
                {
                    button.Text = string.Empty;
                    button.Visible = false;
                    button.BackColor = backColor;
                    button.ForeColor = foreColor;
                }

                try
                {
                    DateTime firstDay = calendar.ToDateTime(currentYear, currentMonth, 1, 0, 0, 0, 0);

                    int daysInMonth = calendar.GetDaysInMonth(currentYear, currentMonth);
                    int startColumn = ((int)firstDay.DayOfWeek + 1) % 7;

                    for (int day = 1; day <= daysInMonth; day++)
                    {
                        int index = startColumn + day - 1;

                        if (index < 0 || index >= dayButtons.Length)
                            continue;

                        Button button = dayButtons[index];

                        button.Text = day.ToString();
                        button.Visible = true;
                        button.Tag = day;

                        int column = grid.GetColumn(button);

                        button.ForeColor = column == 6
                            ? Color.Firebrick
                            : foreColor;

                        if (day == selectedDay)
                        {
                            button.BackColor = accentColor;
                            button.ForeColor = buttonForeColor;
                        }
                    }
                }
                catch
                {
                    MessageBox.Show(
                        "تاریخ انتخابی معتبر نیست",
                        "خطا",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                grid.ResumeLayout(false);
            }

            private void DayButton_Click(object? sender, EventArgs e)
            {
                if (sender is not Button button)
                    return;

                if (button.Tag is not int day)
                    return;

                selectedDay = day;
                UpdateCalendar();
            }

            private void MoveMonth(int offset)
            {
                currentMonth += offset;

                if (currentMonth < 1)
                {
                    currentMonth = 12;
                    currentYear--;
                }
                else if (currentMonth > 12)
                {
                    currentMonth = 1;
                    currentYear++;
                }

                selectedDay = Math.Min(selectedDay, calendar.GetDaysInMonth(currentYear, currentMonth));

                UpdateCalendar();
            }

            private void MoveYear(int offset)
            {
                currentYear += offset;
                selectedDay = Math.Min(selectedDay, calendar.GetDaysInMonth(currentYear, currentMonth));

                UpdateCalendar();
            }

            private void SelectToday()
            {
                DateTime now = DateTime.Now;

                currentYear = calendar.GetYear(now);
                currentMonth = calendar.GetMonth(now);
                selectedDay = calendar.GetDayOfMonth(now);

                UpdateCalendar();
            }

            private void ConfirmSelectedDate()
            {
                if (selectedDay <= 0)
                {
                    MessageBox.Show(
                        "روز انتخاب نشده است",
                        "خطا",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                SelectedDate = $"{currentYear:0000}/{currentMonth:00}/{selectedDay:00}";
                DialogResult = DialogResult.OK;
            }

            private void CalendarPopup_KeyDown(object? sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    e.SuppressKeyPress = true;
                }
            }

            private Button CreateNavButton(string text, Point location)
            {
                Button button = new()
                {
                    Text = text,
                    Location = location,
                    Size = new Size(S(34), S(28)),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = backColor,
                    ForeColor = foreColor,
                    Font = new Font("Tahoma", 9F, FontStyle.Regular),
                    Cursor = Cursors.Hand,
                    TabStop = false,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = hoverColor;
                button.FlatAppearance.MouseDownBackColor = accentColor;

                return button;
            }

            private Button CreateActionButton(string text, Point location)
            {
                Button button = new()
                {
                    Text = text,
                    Location = location,
                    Size = new Size(S(92), S(30)),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = buttonBackColor,
                    ForeColor = buttonForeColor,
                    Font = new Font("Tahoma", 9F, FontStyle.Regular),
                    Cursor = Cursors.Hand,
                    TabStop = false,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = hoverColor;
                button.FlatAppearance.MouseDownBackColor = accentColor;

                return button;
            }
        }
    }
}