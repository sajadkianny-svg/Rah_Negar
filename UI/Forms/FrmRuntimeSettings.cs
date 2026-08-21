using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.UI.Forms.Base;

namespace Rah_Negar.UI.Forms;

public partial class FrmRuntimeSettings : BaseForm
{
    public FrmRuntimeSettings()
    {
        InitializeComponent();

        ApplyAdvancedRuntimeUnitVisibility();
        LoadRuntimeBaseInputs();
        BindNumericTextBoxes();
    }

    /// <summary>
    /// نمایش یا مخفی کردن ورودی‌های واحدها بر اساس تعداد واحدهای موجود در دیتابیس فعال
    /// </summary>
    private void ApplyAdvancedRuntimeUnitVisibility()
    {
        int unitCount = LoadUnitCount();

        SetUnitRuntimeControlsVisible(1, unitCount >= 1);
        SetUnitRuntimeControlsVisible(2, unitCount >= 2);
        SetUnitRuntimeControlsVisible(3, unitCount >= 3);
        SetUnitRuntimeControlsVisible(4, unitCount >= 4);
    }

    /// <summary>
    /// نمایش یا مخفی کردن کنترل‌های مربوط به هر واحد
    /// </summary>
    private void SetUnitRuntimeControlsVisible(int unitNo, bool visible)
    {
        switch (unitNo)
        {
            case 1:
                lblU1.Visible = visible;
                txtU1Run.Visible = visible;
                txtU1OH.Visible = visible;
                break;

            case 2:
                lblU2.Visible = visible;
                txtU2Run.Visible = visible;
                txtU2OH.Visible = visible;
                break;

            case 3:
                lblU3.Visible = visible;
                txtU3Run.Visible = visible;
                txtU3OH.Visible = visible;
                break;

            case 4:
                lblU4.Visible = visible;
                txtU4Run.Visible = visible;
                txtU4OH.Visible = visible;
                break;
        }
    }

    private static int LoadUnitCount()
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = """
        SELECT COUNT(*)
        FROM unit_runtime_base;
        """;

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// مقدارهای فعلی ساعت کارکرد پایه را از دیتابیس خوانده و داخل ورودی‌ها قرار می‌دهد
    /// </summary>
    private void LoadRuntimeBaseInputs()
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = """
        SELECT unit_no, base_runtime_hours, base_runtime_after_oh_hours
        FROM unit_runtime_base
        ORDER BY unit_no;
        """;

        using SqliteDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            int unitNo = Convert.ToInt32(reader["unit_no"]);
            double runtime = Convert.ToDouble(reader["base_runtime_hours"]);
            double afterOh = Convert.ToDouble(reader["base_runtime_after_oh_hours"]);

            SetRuntimeTextBoxes(unitNo, runtime, afterOh);
        }
    }

    private void SetRuntimeTextBoxes(int unitNo, double runtime, double afterOh)
    {
        switch (unitNo)
        {
            case 1:
                txtU1Run.Text = runtime.ToString("0.##");
                txtU1OH.Text = afterOh.ToString("0.##");
                break;

            case 2:
                txtU2Run.Text = runtime.ToString("0.##");
                txtU2OH.Text = afterOh.ToString("0.##");
                break;

            case 3:
                txtU3Run.Text = runtime.ToString("0.##");
                txtU3OH.Text = afterOh.ToString("0.##");
                break;

            case 4:
                txtU4Run.Text = runtime.ToString("0.##");
                txtU4OH.Text = afterOh.ToString("0.##");
                break;
        }
    }

    private void BindNumericTextBoxes()
    {
        TextBox[] boxes =
        [
            txtU1Run, txtU1OH,
            txtU2Run, txtU2OH,
            txtU3Run, txtU3OH,
            txtU4Run, txtU4OH
        ];

        foreach (TextBox box in boxes)
            box.KeyPress += NumericTextBox_KeyPress;
    }

    private static void NumericTextBox_KeyPress(object? sender, KeyPressEventArgs e)
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
    /// مقدارهای پایه ساعت کارکرد و ساعت کارکرد بعد از اورهال را اصلاح و در دیتابیس ذخیره می‌کند
    /// </summary>
    private void btnUpdateRuntimes_Click(object sender, EventArgs e)
    {
        try
        {
            DialogResult result = MessageBox.Show(
                "این بخش برای اصلاح مقدار پایه ساعت کارکرد واحدها استفاده می‌شود" +
                Environment.NewLine +
                Environment.NewLine +
                "تغییر این مقدارها روی گزارش‌ها اثر مستقیم دارد" +
                Environment.NewLine +
                Environment.NewLine +
                "آیا ادامه می‌دهید؟",
                "هشدار تنظیمات کارکرد",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

            if (result != DialogResult.Yes)
                return;

            int unitCount = LoadUnitCount();

            using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
            using SqliteTransaction tx = conn.BeginTransaction();

            if (unitCount >= 1)
                UpdateRuntimeBaseForUnit(conn, tx, 1, txtU1Run, txtU1OH);

            if (unitCount >= 2)
                UpdateRuntimeBaseForUnit(conn, tx, 2, txtU2Run, txtU2OH);

            if (unitCount >= 3)
                UpdateRuntimeBaseForUnit(conn, tx, 3, txtU3Run, txtU3OH);

            if (unitCount >= 4)
                UpdateRuntimeBaseForUnit(conn, tx, 4, txtU4Run, txtU4OH);

            tx.Commit();

            MessageBox.Show(
                "مقادیر پایه ساعت کارکرد با موفقیت ذخیره شد",
                "تنظیمات کارکرد",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "خطا در ذخیره مقدارهای پایه ساعت کارکرد" +
                Environment.NewLine +
                ex.Message,
                "خطا",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// مقدارهای پایه یک واحد را اعتبارسنجی و در جدول unit_runtime_base ذخیره می‌کند
    /// </summary>
    private static void UpdateRuntimeBaseForUnit(
        SqliteConnection conn,
        SqliteTransaction tx,
        int unitNo,
        TextBox txtRuntime,
        TextBox txtAfterOh)
    {
        if (!double.TryParse(txtRuntime.Text.Trim(), out double runtime) || runtime < 0)
            throw new InvalidOperationException($"مقدار Runtime واحد {unitNo} معتبر نیست");

        if (!double.TryParse(txtAfterOh.Text.Trim(), out double afterOh) || afterOh < 0)
            throw new InvalidOperationException($"مقدار Runtime After OH واحد {unitNo} معتبر نیست");

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = """
        UPDATE unit_runtime_base
        SET base_runtime_hours = $runtime,
            base_runtime_after_oh_hours = $afterOh
        WHERE unit_no = $unitNo;
        """;

        cmd.Parameters.AddWithValue("$runtime", runtime);
        cmd.Parameters.AddWithValue("$afterOh", afterOh);
        cmd.Parameters.AddWithValue("$unitNo", unitNo);

        int affected = cmd.ExecuteNonQuery();

        if (affected != 1)
            throw new InvalidOperationException($"رکورد واحد {unitNo} در دیتابیس پیدا نشد");
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        Close();
    }
}