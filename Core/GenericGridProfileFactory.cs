using Rah_Negar.Foundation.Application.Provisioning;

namespace Rah_Negar.Core;

public static class GenericGridProfileFactory
{
    public static GridProfile Create(int unitCount)
    {
        GridProfile profile = Create(CanonicalProfileDefinition.Create(GenericProfileIdentity.Create(unitCount), unitCount));
        // Preserve the pre-canonical fixture contract for callers that still
        // request a synthetic Generic Profile by integer count.
        for (int i = 0; i < profile.Columns.Count; i++)
            profile.Columns[i].Name = $"col{i + 1}";
        return profile;
    }

    public static GridProfile Create(CanonicalProfileDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        int unitCount = definition.UnitCount;

        List<GridColumnProfile> columns =
        [
            new() { Name = "time_rep", HeaderText = "Time", Width = 50, ReadOnly = true },
            new() { Name = "in_p", HeaderText = "Inlet Press.", Width = 55 },
            new() { Name = "out_p", HeaderText = "Outlet Press.", Width = 55 }
        ];

        if (definition.HasLinePressureColumns)
        {
            columns.Add(new() { Name = "line_f_p", HeaderText = "FirstLine Press.", Width = 55 });
            columns.Add(new() { Name = "line40_p", HeaderText = "40in Press.", Width = 45 });
            columns.Add(new() { Name = "line30_p", HeaderText = "30in Press.", Width = 45 });
        }

        List<int> hiddenColumns = [];
        for (int unit = 1; unit <= unitCount; unit++)
        {
            int statusIndex = columns.Count;
            columns.Add(new() { Name = $"u{unit}_st", HeaderText = $"Unit{unit} Status", Width = 58 });
            columns.Add(new() { Name = $"u{unit}_rpm", HeaderText = $"Unit{unit} RPM", Width = 58 });
            hiddenColumns.Add(statusIndex);
        }

        foreach ((string header, int width) in new[]
        {
            ("Recycle", 55),
            ("Flow", 55),
            ("Inlet Temp", 60),
            ("Outlet Temp", 60),
            ("Ambient Temp", 60),
            ("Ratio", 55)
        })
        {
            columns.Add(new()
            {
                Name = header switch
                {
                    "Recycle" => "rec",
                    "Flow" => "flow",
                    "Inlet Temp" => "in_t",
                    "Outlet Temp" => "out_t",
                    "Ambient Temp" => "amb_t",
                    "Ratio" => "ratio",
                    _ => header
                },
                HeaderText = header,
                Width = width
            });
        }

        int ratioIndex = columns.Count - 1;
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
                HeaderFont = new Font("Tahoma", 8.25F, FontStyle.Regular),
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
