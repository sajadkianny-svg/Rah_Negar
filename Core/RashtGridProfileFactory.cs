using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Rah_Negar.Core;

/// <summary>
/// سازنده پروفایل گرید مخصوص Rasht Station
/// </summary>
public static class RashtGridProfileFactory
{
    public static GridProfile Create()
    {
        GridProfile profile = new GridProfile
        {
            HourColumnIndex = 0,
            RatioColumnIndex = 17,
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
                AlternateBackColor2 = Color.FromArgb(255, 255, 255),
                DataRowCount = 12,
                HasAverageRow = true,
                AverageRowIndex = 12
            },
            AverageHiddenColumns = new List<int> { 6, 7, 8, 9, 10, 11, 12 }
        };

        profile.Columns = new List<GridColumnProfile>
        {
            new() { Name = "col1",  HeaderText = "Time",            Width = 45, ReadOnly = true  },
            new() { Name = "col2",  HeaderText = "Inlet Press.",    Width = 50, ReadOnly = false },
            new() { Name = "col3",  HeaderText = "Outlet Press.",   Width = 50, ReadOnly = false },
            new() { Name = "col4",  HeaderText = "FirstLine Press.",Width = 55, ReadOnly = false },
            new() { Name = "col5",  HeaderText = "40in Press.",     Width = 45, ReadOnly = false },
            new() { Name = "col6",  HeaderText = "30in Press.",     Width = 45, ReadOnly = false },
            new() { Name = "col7",  HeaderText = "Unit1 Status",    Width = 45, ReadOnly = false },
            new() { Name = "col8",  HeaderText = "Unit1 RPM",       Width = 45, ReadOnly = false },
            new() { Name = "col9",  HeaderText = "Unit2 Status",    Width = 45, ReadOnly = false },
            new() { Name = "col10", HeaderText = "Unit2 RPM",       Width = 45, ReadOnly = false },
            new() { Name = "col11", HeaderText = "Unit3 Status",    Width = 45, ReadOnly = false },
            new() { Name = "col12", HeaderText = "Unit3 RPM",       Width = 45, ReadOnly = false },
            new() { Name = "col13", HeaderText = "Recycle",         Width = 48, ReadOnly = false },
            new() { Name = "col14", HeaderText = "Flow",            Width = 47, ReadOnly = false },
            new() { Name = "col15", HeaderText = "Inlet Temp",      Width = 50, ReadOnly = false },
            new() { Name = "col16", HeaderText = "Outlet Temp",     Width = 50, ReadOnly = false },
            new() { Name = "col17", HeaderText = "Ambient Temp",    Width = 50, ReadOnly = false },
            new() { Name = "col18", HeaderText = "Ratio",           Width = 45, ReadOnly = false }
        };

        return profile;
    }
}