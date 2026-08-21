namespace Rah_Negar.UI.Forms
{
    partial class FrmRecords
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
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            btnLoad = new Button();
            btnPaste = new Button();
            btnEdit = new Button();
            btnSave = new Button();
            pnlButtom = new Panel();
            btnCancelEdit = new Button();
            btnSaveEdit = new Button();
            btnMissing = new Button();
            btnReset = new Button();
            pnlLine = new Panel();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            pnl8 = new Panel();
            pnlDate = new Panel();
            button1 = new Button();
            pnlDateText = new Panel();
            lblDate = new Label();
            dgvData = new DataGridView();
            tabPage2 = new TabPage();
            pnlBodyEvents = new Panel();
            pnlEvents = new Panel();
            label17 = new Label();
            pnlOperation = new Panel();
            btnEndSelection = new Button();
            btnDeleteItem = new Button();
            btnAdd = new Button();
            label15 = new Label();
            txtRemark = new TextBox();
            label12 = new Label();
            label14 = new Label();
            cmbType = new ComboBox();
            label13 = new Label();
            cmbUnits = new ComboBox();
            dtpTime = new DateTimePicker();
            panel7 = new Panel();
            dgvEvents = new DataGridView();
            colId = new DataGridViewTextBoxColumn();
            colUnit = new DataGridViewTextBoxColumn();
            colEventType = new DataGridViewTextBoxColumn();
            colEventTime = new DataGridViewTextBoxColumn();
            colRemark = new DataGridViewTextBoxColumn();
            pnlBodyUnique = new Panel();
            pnlUnique = new Panel();
            label16 = new Label();
            label6 = new Label();
            label11 = new Label();
            txt_Flow = new TextBox();
            label10 = new Label();
            label3 = new Label();
            label9 = new Label();
            txt_nonFlow = new TextBox();
            label8 = new Label();
            label4 = new Label();
            label7 = new Label();
            txt_irFuel = new TextBox();
            lblGenFuel = new Label();
            txt_Vent = new TextBox();
            txt_TurbineFuel = new TextBox();
            label18 = new Label();
            pnl_Date = new Panel();
            lbl_Date = new Label();
            pnlButtom.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            pnlDate.SuspendLayout();
            pnlDateText.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvData).BeginInit();
            tabPage2.SuspendLayout();
            pnlBodyEvents.SuspendLayout();
            pnlEvents.SuspendLayout();
            pnlOperation.SuspendLayout();
            panel7.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvEvents).BeginInit();
            pnlBodyUnique.SuspendLayout();
            pnlUnique.SuspendLayout();
            pnl_Date.SuspendLayout();
            SuspendLayout();
            // 
            // btnLoad
            // 
            btnLoad.BackColor = Color.WhiteSmoke;
            btnLoad.FlatAppearance.BorderSize = 0;
            btnLoad.FlatAppearance.MouseOverBackColor = Color.White;
            btnLoad.FlatStyle = FlatStyle.Flat;
            btnLoad.Location = new Point(120, 15);
            btnLoad.Name = "btnLoad";
            btnLoad.Size = new Size(98, 26);
            btnLoad.TabIndex = 2;
            btnLoad.Text = "Load";
            btnLoad.UseVisualStyleBackColor = false;
            btnLoad.Click += btnLoad_Click;
            // 
            // btnPaste
            // 
            btnPaste.BackColor = Color.WhiteSmoke;
            btnPaste.FlatAppearance.BorderSize = 0;
            btnPaste.FlatAppearance.MouseOverBackColor = Color.White;
            btnPaste.FlatStyle = FlatStyle.Flat;
            btnPaste.Location = new Point(16, 15);
            btnPaste.Name = "btnPaste";
            btnPaste.Size = new Size(98, 26);
            btnPaste.TabIndex = 3;
            btnPaste.Text = "Paste";
            btnPaste.UseVisualStyleBackColor = false;
            btnPaste.Click += btnPaste_Click;
            // 
            // btnEdit
            // 
            btnEdit.BackColor = Color.WhiteSmoke;
            btnEdit.FlatAppearance.BorderSize = 0;
            btnEdit.FlatAppearance.MouseOverBackColor = Color.White;
            btnEdit.FlatStyle = FlatStyle.Flat;
            btnEdit.Location = new Point(224, 15);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(98, 26);
            btnEdit.TabIndex = 4;
            btnEdit.Text = "Edit";
            btnEdit.UseVisualStyleBackColor = false;
            btnEdit.Click += btnEdit_Click;
            // 
            // btnSave
            // 
            btnSave.BackColor = Color.WhiteSmoke;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.FlatAppearance.MouseOverBackColor = Color.White;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.Location = new Point(536, 15);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(98, 26);
            btnSave.TabIndex = 5;
            btnSave.Text = "Register";
            btnSave.UseVisualStyleBackColor = false;
            btnSave.Click += btnSave_Click;
            // 
            // pnlButtom
            // 
            pnlButtom.BackColor = Color.WhiteSmoke;
            pnlButtom.Controls.Add(btnCancelEdit);
            pnlButtom.Controls.Add(btnSaveEdit);
            pnlButtom.Controls.Add(btnPaste);
            pnlButtom.Controls.Add(btnLoad);
            pnlButtom.Controls.Add(btnEdit);
            pnlButtom.Controls.Add(btnMissing);
            pnlButtom.Controls.Add(btnReset);
            pnlButtom.Controls.Add(btnSave);
            pnlButtom.Controls.Add(pnlLine);
            pnlButtom.Dock = DockStyle.Bottom;
            pnlButtom.Location = new Point(0, 461);
            pnlButtom.Name = "pnlButtom";
            pnlButtom.Size = new Size(880, 50);
            pnlButtom.TabIndex = 1;
            // 
            // btnCancelEdit
            // 
            btnCancelEdit.BackColor = Color.RosyBrown;
            btnCancelEdit.FlatAppearance.BorderSize = 0;
            btnCancelEdit.FlatAppearance.MouseDownBackColor = Color.Red;
            btnCancelEdit.FlatAppearance.MouseOverBackColor = Color.Salmon;
            btnCancelEdit.FlatStyle = FlatStyle.Flat;
            btnCancelEdit.Location = new Point(785, 15);
            btnCancelEdit.Name = "btnCancelEdit";
            btnCancelEdit.Size = new Size(75, 26);
            btnCancelEdit.TabIndex = 15;
            btnCancelEdit.Text = "Cancel";
            btnCancelEdit.UseVisualStyleBackColor = false;
            btnCancelEdit.Click += btnCancelEdit_Click;
            // 
            // btnSaveEdit
            // 
            btnSaveEdit.BackColor = Color.DarkSeaGreen;
            btnSaveEdit.FlatAppearance.BorderSize = 0;
            btnSaveEdit.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 192, 0);
            btnSaveEdit.FlatAppearance.MouseOverBackColor = Color.FromArgb(192, 255, 192);
            btnSaveEdit.FlatStyle = FlatStyle.Flat;
            btnSaveEdit.Location = new Point(707, 15);
            btnSaveEdit.Name = "btnSaveEdit";
            btnSaveEdit.Size = new Size(75, 26);
            btnSaveEdit.TabIndex = 14;
            btnSaveEdit.Text = "Save";
            btnSaveEdit.UseVisualStyleBackColor = false;
            btnSaveEdit.Click += btnSaveEdit_Click;
            // 
            // btnMissing
            // 
            btnMissing.BackColor = Color.WhiteSmoke;
            btnMissing.FlatAppearance.BorderSize = 0;
            btnMissing.FlatAppearance.MouseOverBackColor = Color.White;
            btnMissing.FlatStyle = FlatStyle.Flat;
            btnMissing.Location = new Point(328, 15);
            btnMissing.Name = "btnMissing";
            btnMissing.Size = new Size(98, 26);
            btnMissing.TabIndex = 7;
            btnMissing.Text = "Miss Days";
            btnMissing.UseVisualStyleBackColor = false;
            btnMissing.Click += btnMissing_Click;
            // 
            // btnReset
            // 
            btnReset.BackColor = Color.WhiteSmoke;
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.FlatAppearance.MouseOverBackColor = Color.White;
            btnReset.FlatStyle = FlatStyle.Flat;
            btnReset.Location = new Point(432, 15);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(98, 26);
            btnReset.TabIndex = 6;
            btnReset.Text = "Reset";
            btnReset.UseVisualStyleBackColor = false;
            btnReset.Click += btnReset_Click;
            // 
            // pnlLine
            // 
            pnlLine.Location = new Point(0, 3);
            pnlLine.Name = "pnlLine";
            pnlLine.Size = new Size(884, 3);
            pnlLine.TabIndex = 21;
            // 
            // tabControl1
            // 
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Location = new Point(5, 10);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(884, 454);
            tabControl1.TabIndex = 12;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(pnl8);
            tabPage1.Controls.Add(pnlDate);
            tabPage1.Controls.Add(dgvData);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(876, 426);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Operational Parameters  ";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // pnl8
            // 
            pnl8.Location = new Point(6, 35);
            pnl8.Name = "pnl8";
            pnl8.Size = new Size(853, 4);
            pnl8.TabIndex = 22;
            // 
            // pnlDate
            // 
            pnlDate.BorderStyle = BorderStyle.FixedSingle;
            pnlDate.Controls.Add(button1);
            pnlDate.Controls.Add(pnlDateText);
            pnlDate.ForeColor = SystemColors.ActiveCaptionText;
            pnlDate.Location = new Point(6, 10);
            pnlDate.Name = "pnlDate";
            pnlDate.Size = new Size(853, 25);
            pnlDate.TabIndex = 1;
            // 
            // button1
            // 
            button1.Location = new Point(681, 1);
            button1.Name = "button1";
            button1.Size = new Size(163, 23);
            button1.TabIndex = 16;
            button1.Text = "fake database";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // pnlDateText
            // 
            pnlDateText.BackColor = Color.Gainsboro;
            pnlDateText.Controls.Add(lblDate);
            pnlDateText.Dock = DockStyle.Left;
            pnlDateText.ForeColor = Color.Moccasin;
            pnlDateText.Location = new Point(0, 0);
            pnlDateText.Name = "pnlDateText";
            pnlDateText.Size = new Size(46, 23);
            pnlDateText.TabIndex = 15;
            // 
            // lblDate
            // 
            lblDate.AutoSize = true;
            lblDate.BackColor = Color.Transparent;
            lblDate.ForeColor = Color.WhiteSmoke;
            lblDate.Location = new Point(6, 7);
            lblDate.Name = "lblDate";
            lblDate.Size = new Size(34, 15);
            lblDate.TabIndex = 13;
            lblDate.Text = "Date:";
            // 
            // dgvData
            // 
            dgvData.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvData.BackgroundColor = SystemColors.ButtonFace;
            dgvData.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvData.Location = new Point(6, 42);
            dgvData.Name = "dgvData";
            dgvData.RowHeadersWidth = 51;
            dgvData.Size = new Size(853, 378);
            dgvData.StandardTab = true;
            dgvData.TabIndex = 2;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(pnlBodyEvents);
            tabPage2.Controls.Add(pnlBodyUnique);
            tabPage2.Controls.Add(pnl_Date);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(876, 426);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Fuel & Flow & Events    ";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // pnlBodyEvents
            // 
            pnlBodyEvents.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlBodyEvents.BorderStyle = BorderStyle.FixedSingle;
            pnlBodyEvents.Controls.Add(pnlEvents);
            pnlBodyEvents.Controls.Add(pnlOperation);
            pnlBodyEvents.Controls.Add(panel7);
            pnlBodyEvents.Location = new Point(271, 32);
            pnlBodyEvents.Name = "pnlBodyEvents";
            pnlBodyEvents.Size = new Size(589, 383);
            pnlBodyEvents.TabIndex = 28;
            // 
            // pnlEvents
            // 
            pnlEvents.BackColor = Color.DarkGray;
            pnlEvents.Controls.Add(label17);
            pnlEvents.Dock = DockStyle.Top;
            pnlEvents.Location = new Point(0, 0);
            pnlEvents.Name = "pnlEvents";
            pnlEvents.Size = new Size(587, 20);
            pnlEvents.TabIndex = 29;
            // 
            // label17
            // 
            label17.AutoSize = true;
            label17.Font = new Font("Tahoma", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label17.ForeColor = Color.White;
            label17.Location = new Point(6, 4);
            label17.Name = "label17";
            label17.Size = new Size(45, 13);
            label17.TabIndex = 29;
            label17.Text = "Events";
            // 
            // pnlOperation
            // 
            pnlOperation.Controls.Add(btnEndSelection);
            pnlOperation.Controls.Add(btnDeleteItem);
            pnlOperation.Controls.Add(btnAdd);
            pnlOperation.Controls.Add(label15);
            pnlOperation.Controls.Add(txtRemark);
            pnlOperation.Controls.Add(label12);
            pnlOperation.Controls.Add(label14);
            pnlOperation.Controls.Add(cmbType);
            pnlOperation.Controls.Add(label13);
            pnlOperation.Controls.Add(cmbUnits);
            pnlOperation.Controls.Add(dtpTime);
            pnlOperation.Location = new Point(10, 30);
            pnlOperation.Name = "pnlOperation";
            pnlOperation.Size = new Size(572, 112);
            pnlOperation.TabIndex = 24;
            // 
            // btnEndSelection
            // 
            btnEndSelection.Enabled = false;
            btnEndSelection.FlatStyle = FlatStyle.Flat;
            btnEndSelection.Font = new Font("Tahoma", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnEndSelection.Location = new Point(454, 65);
            btnEndSelection.Name = "btnEndSelection";
            btnEndSelection.Size = new Size(110, 26);
            btnEndSelection.TabIndex = 33;
            btnEndSelection.Text = "Clear Selection";
            btnEndSelection.UseVisualStyleBackColor = true;
            btnEndSelection.Click += btnEndSelection_Click;
            // 
            // btnDeleteItem
            // 
            btnDeleteItem.FlatStyle = FlatStyle.Flat;
            btnDeleteItem.Font = new Font("Tahoma", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnDeleteItem.Location = new Point(454, 36);
            btnDeleteItem.Name = "btnDeleteItem";
            btnDeleteItem.Size = new Size(110, 26);
            btnDeleteItem.TabIndex = 7;
            btnDeleteItem.Text = "Delete";
            btnDeleteItem.UseVisualStyleBackColor = true;
            btnDeleteItem.Click += btnDeleteItem_Click;
            // 
            // btnAdd
            // 
            btnAdd.FlatStyle = FlatStyle.Flat;
            btnAdd.Font = new Font("Tahoma", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnAdd.Location = new Point(454, 7);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(110, 26);
            btnAdd.TabIndex = 6;
            btnAdd.Text = "Add";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAdd_Click;
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label15.Location = new Point(1, 48);
            label15.Name = "label15";
            label15.Size = new Size(51, 14);
            label15.TabIndex = 32;
            label15.Text = "Remark:";
            // 
            // txtRemark
            // 
            txtRemark.BorderStyle = BorderStyle.FixedSingle;
            txtRemark.Enabled = false;
            txtRemark.Location = new Point(0, 65);
            txtRemark.MaxLength = 55;
            txtRemark.Multiline = true;
            txtRemark.Name = "txtRemark";
            txtRemark.Size = new Size(388, 26);
            txtRemark.TabIndex = 31;
            txtRemark.TextAlign = HorizontalAlignment.Center;
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label12.Location = new Point(0, 11);
            label12.Name = "label12";
            label12.Size = new Size(33, 14);
            label12.TabIndex = 21;
            label12.Text = "Unit:";
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label14.Location = new Point(275, 11);
            label14.Name = "label14";
            label14.Size = new Size(38, 14);
            label14.TabIndex = 23;
            label14.Text = "Time:";
            // 
            // cmbType
            // 
            cmbType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbType.FormattingEnabled = true;
            cmbType.Location = new Point(175, 7);
            cmbType.Name = "cmbType";
            cmbType.Size = new Size(90, 23);
            cmbType.TabIndex = 4;
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label13.Location = new Point(136, 11);
            label13.Name = "label13";
            label13.Size = new Size(39, 14);
            label13.TabIndex = 22;
            label13.Text = "Type:";
            // 
            // cmbUnits
            // 
            cmbUnits.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbUnits.FormattingEnabled = true;
            cmbUnits.Location = new Point(35, 7);
            cmbUnits.Name = "cmbUnits";
            cmbUnits.Size = new Size(90, 23);
            cmbUnits.TabIndex = 3;
            // 
            // dtpTime
            // 
            dtpTime.CustomFormat = "\"HH:mm\"";
            dtpTime.Format = DateTimePickerFormat.Time;
            dtpTime.Location = new Point(313, 7);
            dtpTime.Name = "dtpTime";
            dtpTime.ShowUpDown = true;
            dtpTime.Size = new Size(75, 23);
            dtpTime.TabIndex = 5;
            dtpTime.Value = new DateTime(2025, 10, 18, 0, 0, 0, 0);
            // 
            // panel7
            // 
            panel7.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel7.BorderStyle = BorderStyle.FixedSingle;
            panel7.Controls.Add(dgvEvents);
            panel7.ForeColor = SystemColors.ControlLight;
            panel7.Location = new Point(8, 145);
            panel7.Name = "panel7";
            panel7.Size = new Size(573, 230);
            panel7.TabIndex = 16;
            // 
            // dgvEvents
            // 
            dgvEvents.AllowUserToAddRows = false;
            dgvEvents.AllowUserToDeleteRows = false;
            dgvEvents.AllowUserToResizeColumns = false;
            dgvEvents.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = Color.White;
            dgvEvents.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvEvents.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvEvents.BackgroundColor = Color.WhiteSmoke;
            dgvEvents.BorderStyle = BorderStyle.None;
            dgvEvents.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = Color.Gainsboro;
            dataGridViewCellStyle2.Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle2.ForeColor = Color.Black;
            dataGridViewCellStyle2.SelectionBackColor = Color.Gainsboro;
            dataGridViewCellStyle2.SelectionForeColor = Color.Black;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.True;
            dgvEvents.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvEvents.ColumnHeadersHeight = 25;
            dgvEvents.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvEvents.Columns.AddRange(new DataGridViewColumn[] { colId, colUnit, colEventType, colEventTime, colRemark });
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = Color.WhiteSmoke;
            dataGridViewCellStyle3.Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle3.ForeColor = SystemColors.ControlLight;
            dataGridViewCellStyle3.SelectionBackColor = Color.DarkGray;
            dataGridViewCellStyle3.SelectionForeColor = Color.Black;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            dgvEvents.DefaultCellStyle = dataGridViewCellStyle3;
            dgvEvents.EditMode = DataGridViewEditMode.EditOnEnter;
            dgvEvents.EnableHeadersVisualStyles = false;
            dgvEvents.GridColor = Color.White;
            dgvEvents.Location = new Point(0, 0);
            dgvEvents.MultiSelect = false;
            dgvEvents.Name = "dgvEvents";
            dgvEvents.ReadOnly = true;
            dgvEvents.RowHeadersVisible = false;
            dgvEvents.RowHeadersWidth = 51;
            dgvEvents.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dgvEvents.RowTemplate.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvEvents.RowTemplate.DefaultCellStyle.ForeColor = Color.Black;
            dgvEvents.RowTemplate.Resizable = DataGridViewTriState.False;
            dgvEvents.ScrollBars = ScrollBars.None;
            dgvEvents.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvEvents.Size = new Size(571, 228);
            dgvEvents.TabIndex = 30;
            dgvEvents.CellClick += dgvEvents_CellClick;
            // 
            // colId
            // 
            colId.Frozen = true;
            colId.HeaderText = "";
            colId.MinimumWidth = 6;
            colId.Name = "colId";
            colId.ReadOnly = true;
            colId.Resizable = DataGridViewTriState.False;
            colId.Width = 20;
            // 
            // colUnit
            // 
            colUnit.HeaderText = "Unit";
            colUnit.MinimumWidth = 6;
            colUnit.Name = "colUnit";
            colUnit.ReadOnly = true;
            colUnit.Resizable = DataGridViewTriState.False;
            colUnit.SortMode = DataGridViewColumnSortMode.NotSortable;
            colUnit.Width = 50;
            // 
            // colEventType
            // 
            colEventType.HeaderText = "Type";
            colEventType.MinimumWidth = 6;
            colEventType.Name = "colEventType";
            colEventType.ReadOnly = true;
            colEventType.Resizable = DataGridViewTriState.False;
            colEventType.SortMode = DataGridViewColumnSortMode.NotSortable;
            colEventType.Width = 60;
            // 
            // colEventTime
            // 
            colEventTime.HeaderText = "Time";
            colEventTime.MinimumWidth = 6;
            colEventTime.Name = "colEventTime";
            colEventTime.ReadOnly = true;
            colEventTime.Resizable = DataGridViewTriState.False;
            colEventTime.SortMode = DataGridViewColumnSortMode.NotSortable;
            colEventTime.Width = 60;
            // 
            // colRemark
            // 
            colRemark.HeaderText = "Remark";
            colRemark.MinimumWidth = 6;
            colRemark.Name = "colRemark";
            colRemark.ReadOnly = true;
            colRemark.Resizable = DataGridViewTriState.False;
            colRemark.SortMode = DataGridViewColumnSortMode.NotSortable;
            colRemark.Width = 380;
            // 
            // pnlBodyUnique
            // 
            pnlBodyUnique.BorderStyle = BorderStyle.FixedSingle;
            pnlBodyUnique.Controls.Add(pnlUnique);
            pnlBodyUnique.Controls.Add(label6);
            pnlBodyUnique.Controls.Add(label11);
            pnlBodyUnique.Controls.Add(txt_Flow);
            pnlBodyUnique.Controls.Add(label10);
            pnlBodyUnique.Controls.Add(label3);
            pnlBodyUnique.Controls.Add(label9);
            pnlBodyUnique.Controls.Add(txt_nonFlow);
            pnlBodyUnique.Controls.Add(label8);
            pnlBodyUnique.Controls.Add(label4);
            pnlBodyUnique.Controls.Add(label7);
            pnlBodyUnique.Controls.Add(txt_irFuel);
            pnlBodyUnique.Controls.Add(lblGenFuel);
            pnlBodyUnique.Controls.Add(txt_Vent);
            pnlBodyUnique.Controls.Add(txt_TurbineFuel);
            pnlBodyUnique.Controls.Add(label18);
            pnlBodyUnique.Location = new Point(5, 32);
            pnlBodyUnique.Name = "pnlBodyUnique";
            pnlBodyUnique.Size = new Size(266, 383);
            pnlBodyUnique.TabIndex = 15;
            // 
            // pnlUnique
            // 
            pnlUnique.BackColor = Color.DarkGray;
            pnlUnique.Controls.Add(label16);
            pnlUnique.Dock = DockStyle.Top;
            pnlUnique.Location = new Point(0, 0);
            pnlUnique.Name = "pnlUnique";
            pnlUnique.Size = new Size(264, 20);
            pnlUnique.TabIndex = 28;
            // 
            // label16
            // 
            label16.AutoSize = true;
            label16.Font = new Font("Tahoma", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label16.ForeColor = Color.White;
            label16.Location = new Point(5, 4);
            label16.Name = "label16";
            label16.Size = new Size(58, 13);
            label16.TabIndex = 29;
            label16.Text = "Flow Fuel";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(12, 40);
            label6.Name = "label6";
            label6.Size = new Size(49, 15);
            label6.TabIndex = 9;
            label6.Text = "VentGas";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Font = new Font("Tahoma", 8.25F);
            label11.Location = new Point(210, 152);
            label11.Name = "label11";
            label11.Size = new Size(44, 13);
            label11.TabIndex = 14;
            label11.Text = "MMSCM";
            // 
            // txt_Flow
            // 
            txt_Flow.Location = new Point(123, 120);
            txt_Flow.Name = "txt_Flow";
            txt_Flow.ReadOnly = true;
            txt_Flow.Size = new Size(84, 23);
            txt_Flow.TabIndex = 0;
            txt_Flow.TabStop = false;
            txt_Flow.TextAlign = HorizontalAlignment.Center;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Font = new Font("Tahoma", 8.25F);
            label10.Location = new Point(210, 124);
            label10.Name = "label10";
            label10.Size = new Size(44, 13);
            label10.TabIndex = 13;
            label10.Text = "MMSCM";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 124);
            label3.Name = "label3";
            label3.Size = new Size(76, 15);
            label3.TabIndex = 1;
            label3.Text = "Turbine Flow";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Font = new Font("Tahoma", 9F);
            label9.Location = new Point(210, 96);
            label9.Name = "label9";
            label9.Size = new Size(23, 14);
            label9.TabIndex = 12;
            label9.Text = "m³";
            // 
            // txt_nonFlow
            // 
            txt_nonFlow.Location = new Point(123, 148);
            txt_nonFlow.Name = "txt_nonFlow";
            txt_nonFlow.ReadOnly = true;
            txt_nonFlow.Size = new Size(84, 23);
            txt_nonFlow.TabIndex = 2;
            txt_nonFlow.TabStop = false;
            txt_nonFlow.TextAlign = HorizontalAlignment.Center;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Font = new Font("Tahoma", 9F);
            label8.Location = new Point(210, 68);
            label8.Name = "label8";
            label8.Size = new Size(23, 14);
            label8.TabIndex = 11;
            label8.Text = "m³";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(12, 152);
            label4.Name = "label4";
            label4.Size = new Size(104, 15);
            label4.TabIndex = 3;
            label4.Text = "Non-Turbine Flow";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("Tahoma", 9F);
            label7.Location = new Point(210, 40);
            label7.Name = "label7";
            label7.Size = new Size(23, 14);
            label7.TabIndex = 10;
            label7.Text = "m³";
            // 
            // txt_irFuel
            // 
            txt_irFuel.Location = new Point(123, 64);
            txt_irFuel.Name = "txt_irFuel";
            txt_irFuel.Size = new Size(84, 23);
            txt_irFuel.TabIndex = 1;
            txt_irFuel.TextAlign = HorizontalAlignment.Center;
            // 
            // lblGenFuel
            // 
            lblGenFuel.AutoSize = true;
            lblGenFuel.Location = new Point(12, 68);
            lblGenFuel.Name = "lblGenFuel";
            lblGenFuel.Size = new Size(78, 15);
            lblGenFuel.TabIndex = 5;
            lblGenFuel.Text = "Gas Gen. Fuel";
            // 
            // txt_Vent
            // 
            txt_Vent.Location = new Point(123, 36);
            txt_Vent.Name = "txt_Vent";
            txt_Vent.Size = new Size(84, 23);
            txt_Vent.TabIndex = 0;
            txt_Vent.TextAlign = HorizontalAlignment.Center;
            // 
            // txt_TurbineFuel
            // 
            txt_TurbineFuel.Location = new Point(123, 92);
            txt_TurbineFuel.Name = "txt_TurbineFuel";
            txt_TurbineFuel.Size = new Size(84, 23);
            txt_TurbineFuel.TabIndex = 2;
            txt_TurbineFuel.TextAlign = HorizontalAlignment.Center;
            // 
            // label18
            // 
            label18.AutoSize = true;
            label18.Location = new Point(12, 96);
            label18.Name = "label18";
            label18.Size = new Size(76, 15);
            label18.TabIndex = 7;
            label18.Text = "Turbine  Fuel";
            // 
            // pnl_Date
            // 
            pnl_Date.BackColor = Color.Gainsboro;
            pnl_Date.Controls.Add(lbl_Date);
            pnl_Date.ForeColor = Color.Transparent;
            pnl_Date.Location = new Point(5, 11);
            pnl_Date.Name = "pnl_Date";
            pnl_Date.Size = new Size(112, 22);
            pnl_Date.TabIndex = 30;
            // 
            // lbl_Date
            // 
            lbl_Date.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lbl_Date.AutoSize = true;
            lbl_Date.BackColor = Color.Transparent;
            lbl_Date.ForeColor = Color.WhiteSmoke;
            lbl_Date.Location = new Point(24, 4);
            lbl_Date.Name = "lbl_Date";
            lbl_Date.Size = new Size(31, 15);
            lbl_Date.TabIndex = 31;
            lbl_Date.Text = "Date";
            // 
            // FrmRecords
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(880, 511);
            Controls.Add(tabControl1);
            Controls.Add(pnlButtom);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "FrmRecords";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Records";
            Load += FrmRecords_Load;
            pnlButtom.ResumeLayout(false);
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            pnlDate.ResumeLayout(false);
            pnlDateText.ResumeLayout(false);
            pnlDateText.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvData).EndInit();
            tabPage2.ResumeLayout(false);
            pnlBodyEvents.ResumeLayout(false);
            pnlEvents.ResumeLayout(false);
            pnlEvents.PerformLayout();
            pnlOperation.ResumeLayout(false);
            pnlOperation.PerformLayout();
            panel7.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvEvents).EndInit();
            pnlBodyUnique.ResumeLayout(false);
            pnlBodyUnique.PerformLayout();
            pnlUnique.ResumeLayout(false);
            pnlUnique.PerformLayout();
            pnl_Date.ResumeLayout(false);
            pnl_Date.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private Button btnPaste;
        private Button btnLoad;
        private Button btnSave;
        private Button btnEdit;
        private Panel pnlButtom;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private Panel pnlDate;
        private Label lblDate;
        private Panel pnlDateText;
        //==================================================
        private DataGridView dgvData;
        //==================================================
        private TabPage tabPage2;
        private Panel pnlBodyEvents;
        private Panel pnlEvents;
        private Label label17;
        private Panel pnlOperation;
        private Button btnEndSelection;
        private Button btnDeleteItem;
        private Button btnAdd;
        private Label label15;
        private TextBox txtRemark;
        private Label label12;
        private Label label14;
        private ComboBox cmbType;
        private Label label13;
        private ComboBox cmbUnits;
        private DateTimePicker dtpTime;
        private Panel panel7;
        private DataGridView dgvEvents;
        private DataGridViewTextBoxColumn colId;
        private DataGridViewTextBoxColumn colUnit;
        private DataGridViewTextBoxColumn colEventType;
        private DataGridViewTextBoxColumn colEventTime;
        private DataGridViewTextBoxColumn colRemark;
        private Panel pnlBodyUnique;
        private Panel pnlUnique;
        private Label label16;
        private Label label6;
        private Label label11;
        private TextBox txt_Flow;
        private Label label10;
        private Label label3;
        private Label label9;
        private TextBox txt_nonFlow;
        private Label label8;
        private Label label4;
        private Label label7;
        private TextBox txt_irFuel;
        private Label lblGenFuel;
        private TextBox txt_Vent;
        private TextBox txt_TurbineFuel;
        private Label label18;
        private Panel pnl_Date;
        private Button btnMissing;
        private Button btnSaveEdit;
        private Button btnReset;
        private Panel pnlLine;
        private Panel pnl8;
        private Label lbl_Date;
        private Button btnCancelEdit;
        private Button button1;
    }
}