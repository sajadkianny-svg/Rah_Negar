namespace Rah_Negar.UI.Forms
{
    partial class FrmReportCenter
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            pnlHeader = new Panel();
            lblTitle = new Label();
            pnlFilterCard = new Panel();
            btnPDF = new Button();
            btnFinalizeMonthlyReport = new Button();
            rdoSecondHalf = new RadioButton();
            rdoFirstHalf = new RadioButton();
            rdoYearly = new RadioButton();
            rdoMonthly = new RadioButton();
            btnGenerateReport = new Button();
            label2 = new Label();
            label1 = new Label();
            cmbMonth = new ComboBox();
            cmbYear = new ComboBox();
            pnlNavigation = new Panel();
            btnLogPage = new Button();
            rdoLogByEvent = new RadioButton();
            rdoLogByUnit = new RadioButton();
            btnServicePage = new Button();
            btnEventsPage = new Button();
            btnSummaryPage = new Button();
            pnlContent = new Panel();
            pnlServicePage = new Panel();
            pnlServiceBottom = new Panel();
            dgvServiceCombination = new DataGridView();
            pnlServiceTop = new Panel();
            dgvServiceDays = new DataGridView();
            pnlSummaryPage = new Panel();
            pnlRight = new Panel();
            dgvExtremeDates = new DataGridView();
            pnlDivider = new Panel();
            pnlLeft = new Panel();
            dgvSummary = new DataGridView();
            pnlEventsPage = new Panel();
            dgvEventSummary = new DataGridView();
            dgvUniqueSummary = new DataGridView();
            pnlLogPage = new Panel();
            dgvEventLog = new DataGridView();
            pnlHeader.SuspendLayout();
            pnlFilterCard.SuspendLayout();
            pnlNavigation.SuspendLayout();
            pnlContent.SuspendLayout();
            pnlServicePage.SuspendLayout();
            pnlServiceBottom.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvServiceCombination).BeginInit();
            pnlServiceTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvServiceDays).BeginInit();
            pnlSummaryPage.SuspendLayout();
            pnlRight.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvExtremeDates).BeginInit();
            pnlLeft.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvSummary).BeginInit();
            pnlEventsPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvEventSummary).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvUniqueSummary).BeginInit();
            pnlLogPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvEventLog).BeginInit();
            SuspendLayout();
            // 
            // pnlHeader
            // 
            pnlHeader.BackColor = Color.SteelBlue;
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new Size(784, 48);
            pnlHeader.TabIndex = 0;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(16, 18);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(123, 13);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Analytics Dashboard";
            // 
            // pnlFilterCard
            // 
            pnlFilterCard.BackColor = Color.White;
            pnlFilterCard.BorderStyle = BorderStyle.FixedSingle;
            pnlFilterCard.Controls.Add(btnPDF);
            pnlFilterCard.Controls.Add(btnFinalizeMonthlyReport);
            pnlFilterCard.Controls.Add(rdoSecondHalf);
            pnlFilterCard.Controls.Add(rdoFirstHalf);
            pnlFilterCard.Controls.Add(rdoYearly);
            pnlFilterCard.Controls.Add(rdoMonthly);
            pnlFilterCard.Controls.Add(btnGenerateReport);
            pnlFilterCard.Controls.Add(label2);
            pnlFilterCard.Controls.Add(label1);
            pnlFilterCard.Controls.Add(cmbMonth);
            pnlFilterCard.Controls.Add(cmbYear);
            pnlFilterCard.Dock = DockStyle.Top;
            pnlFilterCard.Location = new Point(0, 48);
            pnlFilterCard.Name = "pnlFilterCard";
            pnlFilterCard.Padding = new Padding(10, 8, 10, 8);
            pnlFilterCard.Size = new Size(784, 62);
            pnlFilterCard.TabIndex = 1;
            // 
            // btnPDF
            // 
            btnPDF.BackColor = Color.DodgerBlue;
            btnPDF.Cursor = Cursors.Hand;
            btnPDF.FlatAppearance.MouseDownBackColor = Color.SlateBlue;
            btnPDF.FlatAppearance.MouseOverBackColor = Color.RoyalBlue;
            btnPDF.FlatStyle = FlatStyle.Flat;
            btnPDF.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            btnPDF.ForeColor = Color.White;
            btnPDF.Location = new Point(671, 7);
            btnPDF.Name = "btnPDF";
            btnPDF.Size = new Size(105, 27);
            btnPDF.TabIndex = 34;
            btnPDF.Text = "PDF Report";
            btnPDF.UseVisualStyleBackColor = false;
            btnPDF.Click += btnPDF_Click;
            // 
            // btnFinalizeMonthlyReport
            // 
            btnFinalizeMonthlyReport.BackColor = Color.DodgerBlue;
            btnFinalizeMonthlyReport.Cursor = Cursors.Hand;
            btnFinalizeMonthlyReport.FlatAppearance.MouseDownBackColor = Color.SlateBlue;
            btnFinalizeMonthlyReport.FlatAppearance.MouseOverBackColor = Color.RoyalBlue;
            btnFinalizeMonthlyReport.FlatStyle = FlatStyle.Flat;
            btnFinalizeMonthlyReport.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            btnFinalizeMonthlyReport.ForeColor = Color.White;
            btnFinalizeMonthlyReport.Location = new Point(561, 7);
            btnFinalizeMonthlyReport.Name = "btnFinalizeMonthlyReport";
            btnFinalizeMonthlyReport.Size = new Size(105, 27);
            btnFinalizeMonthlyReport.TabIndex = 33;
            btnFinalizeMonthlyReport.Text = "Finalize Month";
            btnFinalizeMonthlyReport.UseVisualStyleBackColor = false;
            btnFinalizeMonthlyReport.Click += btnFinalizeMonthlyReport_Click;
            // 
            // rdoSecondHalf
            // 
            rdoSecondHalf.AutoSize = true;
            rdoSecondHalf.Font = new Font("Tahoma", 8.5F);
            rdoSecondHalf.Location = new Point(283, 35);
            rdoSecondHalf.Name = "rdoSecondHalf";
            rdoSecondHalf.Size = new Size(70, 18);
            rdoSecondHalf.TabIndex = 32;
            rdoSecondHalf.Text = "2nd Half";
            rdoSecondHalf.UseVisualStyleBackColor = true;
            rdoSecondHalf.CheckedChanged += ReportMode_CheckedChanged;
            // 
            // rdoFirstHalf
            // 
            rdoFirstHalf.AutoSize = true;
            rdoFirstHalf.Font = new Font("Tahoma", 8.5F);
            rdoFirstHalf.Location = new Point(203, 35);
            rdoFirstHalf.Name = "rdoFirstHalf";
            rdoFirstHalf.Size = new Size(66, 18);
            rdoFirstHalf.TabIndex = 31;
            rdoFirstHalf.Text = "1st Half";
            rdoFirstHalf.UseVisualStyleBackColor = true;
            rdoFirstHalf.CheckedChanged += ReportMode_CheckedChanged;
            // 
            // rdoYearly
            // 
            rdoYearly.AutoSize = true;
            rdoYearly.Font = new Font("Tahoma", 8.5F);
            rdoYearly.Location = new Point(283, 9);
            rdoYearly.Name = "rdoYearly";
            rdoYearly.Size = new Size(58, 18);
            rdoYearly.TabIndex = 30;
            rdoYearly.Text = "Yearly";
            rdoYearly.UseVisualStyleBackColor = true;
            rdoYearly.CheckedChanged += ReportMode_CheckedChanged;
            // 
            // rdoMonthly
            // 
            rdoMonthly.AutoSize = true;
            rdoMonthly.Checked = true;
            rdoMonthly.Font = new Font("Tahoma", 8.5F);
            rdoMonthly.Location = new Point(203, 9);
            rdoMonthly.Name = "rdoMonthly";
            rdoMonthly.Size = new Size(68, 18);
            rdoMonthly.TabIndex = 29;
            rdoMonthly.TabStop = true;
            rdoMonthly.Text = "Monthly";
            rdoMonthly.UseVisualStyleBackColor = true;
            rdoMonthly.CheckedChanged += ReportMode_CheckedChanged;
            // 
            // btnGenerateReport
            // 
            btnGenerateReport.BackColor = Color.DodgerBlue;
            btnGenerateReport.Cursor = Cursors.Hand;
            btnGenerateReport.FlatAppearance.MouseDownBackColor = Color.SlateBlue;
            btnGenerateReport.FlatAppearance.MouseOverBackColor = Color.RoyalBlue;
            btnGenerateReport.FlatStyle = FlatStyle.Flat;
            btnGenerateReport.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            btnGenerateReport.ForeColor = Color.White;
            btnGenerateReport.Location = new Point(426, 7);
            btnGenerateReport.Name = "btnGenerateReport";
            btnGenerateReport.Size = new Size(130, 27);
            btnGenerateReport.TabIndex = 0;
            btnGenerateReport.Text = "Run Analysis";
            btnGenerateReport.UseVisualStyleBackColor = false;
            btnGenerateReport.Click += btnGenerateReport_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Tahoma", 9F);
            label2.ForeColor = Color.Gray;
            label2.Location = new Point(16, 38);
            label2.Name = "label2";
            label2.Size = new Size(46, 14);
            label2.TabIndex = 27;
            label2.Text = "Month:";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Tahoma", 9F);
            label1.ForeColor = Color.Gray;
            label1.Location = new Point(16, 10);
            label1.Name = "label1";
            label1.Size = new Size(36, 14);
            label1.TabIndex = 26;
            label1.Text = "Year:";
            // 
            // cmbMonth
            // 
            cmbMonth.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMonth.FormattingEnabled = true;
            cmbMonth.Location = new Point(66, 33);
            cmbMonth.Name = "cmbMonth";
            cmbMonth.Size = new Size(85, 23);
            cmbMonth.TabIndex = 25;
            cmbMonth.SelectedIndexChanged += cmbMonth_SelectedIndexChanged;
            // 
            // cmbYear
            // 
            cmbYear.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbYear.FormattingEnabled = true;
            cmbYear.Location = new Point(66, 7);
            cmbYear.Name = "cmbYear";
            cmbYear.Size = new Size(85, 23);
            cmbYear.TabIndex = 24;
            cmbYear.SelectedIndexChanged += cmbYear_SelectedIndexChanged;
            // 
            // pnlNavigation
            // 
            pnlNavigation.Controls.Add(btnLogPage);
            pnlNavigation.Controls.Add(rdoLogByEvent);
            pnlNavigation.Controls.Add(rdoLogByUnit);
            pnlNavigation.Controls.Add(btnServicePage);
            pnlNavigation.Controls.Add(btnEventsPage);
            pnlNavigation.Controls.Add(btnSummaryPage);
            pnlNavigation.Dock = DockStyle.Top;
            pnlNavigation.Location = new Point(0, 110);
            pnlNavigation.Name = "pnlNavigation";
            pnlNavigation.Padding = new Padding(10, 5, 10, 5);
            pnlNavigation.Size = new Size(784, 38);
            pnlNavigation.TabIndex = 2;
            // 
            // btnLogPage
            // 
            btnLogPage.BackColor = Color.White;
            btnLogPage.Cursor = Cursors.Hand;
            btnLogPage.FlatAppearance.MouseDownBackColor = Color.Gainsboro;
            btnLogPage.FlatAppearance.MouseOverBackColor = Color.LightBlue;
            btnLogPage.FlatStyle = FlatStyle.Flat;
            btnLogPage.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            btnLogPage.ForeColor = Color.Black;
            btnLogPage.Location = new Point(379, 6);
            btnLogPage.Name = "btnLogPage";
            btnLogPage.Size = new Size(120, 26);
            btnLogPage.TabIndex = 3;
            btnLogPage.TabStop = false;
            btnLogPage.Text = "Event Log";
            btnLogPage.UseVisualStyleBackColor = false;
            btnLogPage.Click += btnLogPage_Click;
            // 
            // rdoLogByEvent
            // 
            rdoLogByEvent.AutoSize = true;
            rdoLogByEvent.BackColor = Color.Transparent;
            rdoLogByEvent.Checked = true;
            rdoLogByEvent.Location = new Point(632, 9);
            rdoLogByEvent.Name = "rdoLogByEvent";
            rdoLogByEvent.Size = new Size(67, 19);
            rdoLogByEvent.TabIndex = 2;
            rdoLogByEvent.TabStop = true;
            rdoLogByEvent.Text = "ByEvent";
            rdoLogByEvent.UseVisualStyleBackColor = false;
            rdoLogByEvent.Visible = false;
            rdoLogByEvent.CheckedChanged += EventLogMode_CheckedChanged;
            // 
            // rdoLogByUnit
            // 
            rdoLogByUnit.AutoSize = true;
            rdoLogByUnit.BackColor = Color.Transparent;
            rdoLogByUnit.Location = new Point(711, 9);
            rdoLogByUnit.Name = "rdoLogByUnit";
            rdoLogByUnit.Size = new Size(60, 19);
            rdoLogByUnit.TabIndex = 1;
            rdoLogByUnit.TabStop = true;
            rdoLogByUnit.Text = "ByUnit";
            rdoLogByUnit.UseVisualStyleBackColor = false;
            rdoLogByUnit.Visible = false;
            rdoLogByUnit.CheckedChanged += EventLogMode_CheckedChanged;
            // 
            // btnServicePage
            // 
            btnServicePage.BackColor = Color.White;
            btnServicePage.Cursor = Cursors.Hand;
            btnServicePage.FlatAppearance.MouseDownBackColor = Color.Gainsboro;
            btnServicePage.FlatAppearance.MouseOverBackColor = Color.LightBlue;
            btnServicePage.FlatStyle = FlatStyle.Flat;
            btnServicePage.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            btnServicePage.ForeColor = Color.Black;
            btnServicePage.Location = new Point(256, 6);
            btnServicePage.Name = "btnServicePage";
            btnServicePage.Size = new Size(120, 26);
            btnServicePage.TabIndex = 2;
            btnServicePage.TabStop = false;
            btnServicePage.Text = "Service Analysis";
            btnServicePage.UseVisualStyleBackColor = false;
            btnServicePage.Click += btnServicePage_Click;
            // 
            // btnEventsPage
            // 
            btnEventsPage.BackColor = Color.White;
            btnEventsPage.Cursor = Cursors.Hand;
            btnEventsPage.FlatAppearance.MouseDownBackColor = Color.Gainsboro;
            btnEventsPage.FlatAppearance.MouseOverBackColor = Color.LightBlue;
            btnEventsPage.FlatStyle = FlatStyle.Flat;
            btnEventsPage.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            btnEventsPage.ForeColor = Color.Black;
            btnEventsPage.Location = new Point(133, 6);
            btnEventsPage.Name = "btnEventsPage";
            btnEventsPage.Size = new Size(120, 26);
            btnEventsPage.TabIndex = 1;
            btnEventsPage.TabStop = false;
            btnEventsPage.Text = "Event Summary";
            btnEventsPage.UseVisualStyleBackColor = false;
            btnEventsPage.Click += btnEventsPage_Click;
            // 
            // btnSummaryPage
            // 
            btnSummaryPage.BackColor = Color.DodgerBlue;
            btnSummaryPage.Cursor = Cursors.Hand;
            btnSummaryPage.FlatStyle = FlatStyle.Flat;
            btnSummaryPage.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            btnSummaryPage.ForeColor = Color.White;
            btnSummaryPage.Location = new Point(10, 6);
            btnSummaryPage.Name = "btnSummaryPage";
            btnSummaryPage.Size = new Size(120, 26);
            btnSummaryPage.TabIndex = 0;
            btnSummaryPage.TabStop = false;
            btnSummaryPage.Text = "Overview";
            btnSummaryPage.UseVisualStyleBackColor = false;
            btnSummaryPage.Click += btnSummaryPage_Click;
            // 
            // pnlContent
            // 
            pnlContent.Controls.Add(pnlServicePage);
            pnlContent.Controls.Add(pnlSummaryPage);
            pnlContent.Controls.Add(pnlEventsPage);
            pnlContent.Controls.Add(pnlLogPage);
            pnlContent.Dock = DockStyle.Fill;
            pnlContent.Location = new Point(0, 148);
            pnlContent.Name = "pnlContent";
            pnlContent.Padding = new Padding(10, 6, 10, 10);
            pnlContent.Size = new Size(784, 313);
            pnlContent.TabIndex = 3;
            // 
            // pnlServicePage
            // 
            pnlServicePage.Controls.Add(pnlServiceBottom);
            pnlServicePage.Controls.Add(pnlServiceTop);
            pnlServicePage.Dock = DockStyle.Fill;
            pnlServicePage.Location = new Point(10, 6);
            pnlServicePage.Name = "pnlServicePage";
            pnlServicePage.Size = new Size(764, 297);
            pnlServicePage.TabIndex = 2;
            // 
            // pnlServiceBottom
            // 
            pnlServiceBottom.Controls.Add(dgvServiceCombination);
            pnlServiceBottom.Location = new Point(0, 140);
            pnlServiceBottom.Name = "pnlServiceBottom";
            pnlServiceBottom.Size = new Size(764, 157);
            pnlServiceBottom.TabIndex = 1;
            // 
            // dgvServiceCombination
            // 
            dgvServiceCombination.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvServiceCombination.Dock = DockStyle.Fill;
            dgvServiceCombination.Location = new Point(0, 0);
            dgvServiceCombination.Name = "dgvServiceCombination";
            dgvServiceCombination.Size = new Size(764, 157);
            dgvServiceCombination.TabIndex = 0;
            // 
            // pnlServiceTop
            // 
            pnlServiceTop.Controls.Add(dgvServiceDays);
            pnlServiceTop.Location = new Point(0, 0);
            pnlServiceTop.Name = "pnlServiceTop";
            pnlServiceTop.Size = new Size(764, 125);
            pnlServiceTop.TabIndex = 0;
            // 
            // dgvServiceDays
            // 
            dgvServiceDays.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvServiceDays.Dock = DockStyle.Fill;
            dgvServiceDays.Location = new Point(0, 0);
            dgvServiceDays.Name = "dgvServiceDays";
            dgvServiceDays.Size = new Size(764, 125);
            dgvServiceDays.TabIndex = 2;
            // 
            // pnlSummaryPage
            // 
            pnlSummaryPage.Controls.Add(pnlRight);
            pnlSummaryPage.Controls.Add(pnlDivider);
            pnlSummaryPage.Controls.Add(pnlLeft);
            pnlSummaryPage.Dock = DockStyle.Fill;
            pnlSummaryPage.Location = new Point(10, 6);
            pnlSummaryPage.Name = "pnlSummaryPage";
            pnlSummaryPage.Size = new Size(764, 297);
            pnlSummaryPage.TabIndex = 6;
            // 
            // pnlRight
            // 
            pnlRight.Controls.Add(dgvExtremeDates);
            pnlRight.Dock = DockStyle.Right;
            pnlRight.Location = new Point(384, 0);
            pnlRight.Name = "pnlRight";
            pnlRight.Size = new Size(380, 297);
            pnlRight.TabIndex = 3;
            // 
            // dgvExtremeDates
            // 
            dgvExtremeDates.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvExtremeDates.Dock = DockStyle.Fill;
            dgvExtremeDates.Location = new Point(0, 0);
            dgvExtremeDates.Name = "dgvExtremeDates";
            dgvExtremeDates.Size = new Size(380, 297);
            dgvExtremeDates.TabIndex = 4;
            // 
            // pnlDivider
            // 
            pnlDivider.Location = new Point(388, 0);
            pnlDivider.Name = "pnlDivider";
            pnlDivider.Size = new Size(4, 297);
            pnlDivider.TabIndex = 2;
            // 
            // pnlLeft
            // 
            pnlLeft.Controls.Add(dgvSummary);
            pnlLeft.Dock = DockStyle.Left;
            pnlLeft.Location = new Point(0, 0);
            pnlLeft.Name = "pnlLeft";
            pnlLeft.Size = new Size(380, 297);
            pnlLeft.TabIndex = 2;
            // 
            // dgvSummary
            // 
            dgvSummary.BackgroundColor = Color.White;
            dgvSummary.BorderStyle = BorderStyle.None;
            dgvSummary.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvSummary.Dock = DockStyle.Fill;
            dgvSummary.Location = new Point(0, 0);
            dgvSummary.Name = "dgvSummary";
            dgvSummary.Size = new Size(380, 297);
            dgvSummary.TabIndex = 1;
            // 
            // pnlEventsPage
            // 
            pnlEventsPage.Controls.Add(dgvEventSummary);
            pnlEventsPage.Controls.Add(dgvUniqueSummary);
            pnlEventsPage.Dock = DockStyle.Fill;
            pnlEventsPage.Location = new Point(10, 6);
            pnlEventsPage.Name = "pnlEventsPage";
            pnlEventsPage.Size = new Size(764, 297);
            pnlEventsPage.TabIndex = 5;
            pnlEventsPage.Visible = false;
            // 
            // dgvEventSummary
            // 
            dgvEventSummary.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvEventSummary.Dock = DockStyle.Left;
            dgvEventSummary.Location = new Point(0, 0);
            dgvEventSummary.Name = "dgvEventSummary";
            dgvEventSummary.ScrollBars = ScrollBars.Vertical;
            dgvEventSummary.Size = new Size(490, 297);
            dgvEventSummary.TabIndex = 2;
            // 
            // dgvUniqueSummary
            // 
            dgvUniqueSummary.BackgroundColor = Color.White;
            dgvUniqueSummary.BorderStyle = BorderStyle.None;
            dgvUniqueSummary.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvUniqueSummary.Dock = DockStyle.Right;
            dgvUniqueSummary.Location = new Point(496, 0);
            dgvUniqueSummary.Name = "dgvUniqueSummary";
            dgvUniqueSummary.Size = new Size(268, 297);
            dgvUniqueSummary.TabIndex = 0;
            // 
            // pnlLogPage
            // 
            pnlLogPage.Controls.Add(dgvEventLog);
            pnlLogPage.Dock = DockStyle.Fill;
            pnlLogPage.Location = new Point(10, 6);
            pnlLogPage.Name = "pnlLogPage";
            pnlLogPage.Size = new Size(764, 297);
            pnlLogPage.TabIndex = 3;
            pnlLogPage.Visible = false;
            // 
            // dgvEventLog
            // 
            dgvEventLog.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvEventLog.Location = new Point(0, 0);
            dgvEventLog.Name = "dgvEventLog";
            dgvEventLog.Size = new Size(764, 297);
            dgvEventLog.TabIndex = 1;
            // 
            // FrmReportCenter
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.WhiteSmoke;
            ClientSize = new Size(784, 461);
            Controls.Add(pnlContent);
            Controls.Add(pnlNavigation);
            Controls.Add(pnlFilterCard);
            Controls.Add(pnlHeader);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            KeyPreview = true;
            MaximizeBox = false;
            Name = "FrmReportCenter";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ReportCenter";
            Load += FrmReportCenter_Load_1;
            pnlHeader.ResumeLayout(false);
            pnlHeader.PerformLayout();
            pnlFilterCard.ResumeLayout(false);
            pnlFilterCard.PerformLayout();
            pnlNavigation.ResumeLayout(false);
            pnlNavigation.PerformLayout();
            pnlContent.ResumeLayout(false);
            pnlServicePage.ResumeLayout(false);
            pnlServiceBottom.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvServiceCombination).EndInit();
            pnlServiceTop.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvServiceDays).EndInit();
            pnlSummaryPage.ResumeLayout(false);
            pnlRight.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvExtremeDates).EndInit();
            pnlLeft.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvSummary).EndInit();
            pnlEventsPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvEventSummary).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvUniqueSummary).EndInit();
            pnlLogPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvEventLog).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnlHeader;
        private Label lblTitle;
        private Panel pnlFilterCard;
        private Panel pnlNavigation;
        private Label label2;
        private Label label1;
        private ComboBox cmbMonth;
        private ComboBox cmbYear;
        private Button btnGenerateReport;
        private RadioButton rdoSecondHalf;
        private RadioButton rdoFirstHalf;
        private RadioButton rdoYearly;
        private RadioButton rdoMonthly;
        private Button btnSummaryPage;
        private Button btnServicePage;
        private Button btnEventsPage;
        private Panel pnlContent;
        private Panel pnlSummaryPage;
        private DataGridView dgvSummary;
        private DataGridView dgvUniqueSummary;
        private Panel pnlEventsPage;
        private Panel pnlLogPage;
        private Panel pnlRight;
        private Panel pnlLeft;
        private DataGridView dgvEventSummary;
        private Panel pnlDivider;
        private DataGridView dgvServiceDays;
        private DataGridView dgvEventLog;
        private RadioButton rdoLogByEvent;
        private RadioButton rdoLogByUnit;
        private Button btnLogPage;
        private Panel pnlServicePage;
        private Panel pnlServiceTop;
        private Panel pnlServiceBottom;
        private DataGridView dgvServiceCombination;
        private DataGridView dgvExtremeDates;
        private Button btnFinalizeMonthlyReport;
        private Button btnPDF;
    }
}