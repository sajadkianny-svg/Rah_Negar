using System.Windows.Forms;

namespace Rah_Negar.Utils;

/// <summary>
/// سرویس مرکزی تنظیمات ظاهری و DPI مربوط به DataGridView.
/// هدف این است که گریدها در DPIهای مختلف رفتار یکسان و قابل کنترل داشته باشند.
/// </summary>
public static class DataGridViewUiService
{
    /// <summary>
    /// تنظیمات عمومی برای گریدهای صنعتی برنامه.
    /// </summary>
    public static void ConfigureBaseGrid(
        DataGridView dgv,
        Control owner,
        bool allowHorizontalScroll)
    {
        if (dgv == null)
            return;

        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

        dgv.ScrollBars = allowHorizontalScroll
            ? ScrollBars.Both
            : ScrollBars.Vertical;

        dgv.EnableHeadersVisualStyles = false;

        dgv.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

        dgv.ColumnHeadersHeightSizeMode =
            DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        dgv.RowHeadersWidthSizeMode =
            DataGridViewRowHeadersWidthSizeMode.DisableResizing;

        dgv.Font = UiScaleService.GetDefaultFont(owner, 8.5f);
        dgv.DefaultCellStyle.Font = UiScaleService.GetDefaultFont(owner, 8.5f);
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.2f, FontStyle.Bold, GraphicsUnit.Point);

        dgv.RowTemplate.Height = UiScaleService.Scale(owner, 24);
    }

    /// <summary>
    /// ارتفاع Header گرید را تنظیم می‌کند.
    /// برای گریدهایی که عنوان ستون‌ها دوخطی هستند، مقدار بیشتری بده.
    /// </summary>
    public static void SetHeaderHeight(
        DataGridView dgv,
        Control owner,
        int baseHeight)
    {
        if (dgv == null)
            return;

        dgv.ColumnHeadersHeightSizeMode =
            DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        dgv.ColumnHeadersHeight = UiScaleService.Scale(owner, baseHeight);
    }

    /// <summary>
    /// عرض ستون‌ها را بر اساس عرض‌های پایه و عرض واقعی گرید Fit می‌کند.
    /// بدون استفاده از Fill؛ مناسب برای گریدهای دارای ستون Frozen یا ستون‌های حساس.
    /// </summary>
    public static void FitColumnsByBaseWidths(
        DataGridView dgv,
        IReadOnlyList<int> baseWidths,
        int minimumWidth = 25)
    {
        if (dgv == null)
            return;

        if (dgv.Columns.Count == 0 || baseWidths.Count == 0)
            return;

        int count = Math.Min(dgv.Columns.Count, baseWidths.Count);

        int availableWidth = dgv.ClientSize.Width - 8;

        if (availableWidth <= 0)
            return;

        int totalBaseWidth = baseWidths.Take(count).Sum();

        if (totalBaseWidth <= 0)
            return;

        int usedWidth = 0;

        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

        for (int i = 0; i < count; i++)
        {
            DataGridViewColumn column = dgv.Columns[i];

            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.MinimumWidth = minimumWidth;

            int newWidth;

            if (i == count - 1)
            {
                newWidth = availableWidth - usedWidth;
            }
            else
            {
                newWidth = (int)Math.Floor(
                    availableWidth * (baseWidths[i] / (double)totalBaseWidth));

                usedWidth += newWidth;
            }

            column.Width = Math.Max(minimumWidth, newWidth);
        }
    }
}