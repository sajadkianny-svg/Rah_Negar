using Rah_Negar.Foundation.Application.Provisioning;

namespace Rah_Negar.Core;

public static class GenericGridProfileFactory
{
    public static GridProfile Create(int unitCount)
    {
        if (!TargetStationProfileRules.IsUnitCountSupported(unitCount))
            throw new ArgumentOutOfRangeException(nameof(unitCount));

        List<GridColumnProfile> columns =
        [
            new() { Name = "col1", HeaderText = "Time", Width = 50, ReadOnly = true },
            new() { Name = "col2", HeaderText = "Inlet Press.", Width = 55 },
            new() { Name = "col3", HeaderText = "Outlet Press.", Width = 55 }
        ];

        List<int> averageColumns = [1, 2];
        List<int> hiddenColumns = [];
        for (int unit = 1; unit <= unitCount; unit++)
        {
            int statusIndex = columns.Count;
            columns.Add(new() { Name = $"col{columns.Count + 1}", HeaderText = $"Unit{unit} Status", Width = 58 });
            columns.Add(new() { Name = $"col{columns.Count + 1}", HeaderText = $"Unit{unit} RPM", Width = 58 });
            hiddenColumns.Add(statusIndex);
        }

        int ratioIndex = columns.Count;
        columns.AddRange([
            new() { Name = $"col{columns.Count + 1}", HeaderText = "Recycle", Width = 55 },
            new() { Name = $"col{columns.Count + 1}", HeaderText = "Flow", Width = 55 },
            new() { Name = $"col{columns.Count + 1}", HeaderText = "Inlet Temp", Width = 60 },
            new() { Name = $"col{columns.Count + 1}", HeaderText = "Outlet Temp", Width = 60 },
            new() { Name = $"col{columns.Count + 1}", HeaderText = "Ambient Temp", Width = 60 },
            new() { Name = $"col{columns.Count + 1}", HeaderText = "Ratio", Width = 55 }]);
        averageColumns.AddRange([ratioIndex - 5, ratioIndex - 4, ratioIndex - 3, ratioIndex - 2, ratioIndex - 1, ratioIndex]);

        return new GridProfile
        {
            HourColumnIndex = 0,
            RatioColumnIndex = ratioIndex,
            Columns = columns,
            AverageHiddenColumns = hiddenColumns,
            Visual = new GridVisualProfile
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = false,
                AllowUserToResizeColumns = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnKeystroke,
                HeaderBackColor = Color.LightGray,
                HeaderForeColor = Color.Black,
                HeaderFont = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                HeaderHeight = 50,
                GridColor = Color.LightGray,
                SelectionBackColor = Color.FromArgb(135, 206, 250),
                SelectionForeColor = Color.Black,
                AlternateBackColor1 = Color.FromArgb(245, 245, 245),
                AlternateBackColor2 = Color.White,
                DataRowCount = 12,
                HasAverageRow = true,
                AverageRowIndex = 12
            }
        };
    }
}
