using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Rah_Negar.Core;

/// <summary>
/// سازنده پروفایل گرید مخصوص Ramsar Station.
/// تفاوت با Rasht:
/// - حذف سه ستون line_f_p و line40_p و line30_p
/// - اضافه شدن Unit4
/// - توزیع +55 عرض بین ستون‌های غیر Status و غیر RPM
/// </summary>
public static class RamsarGridProfileFactory
{
    /// <summary>
    /// GridProfile اختصاصی Ramsar Station را برمی‌گرداند.
    /// </summary>
    public static GridProfile Create()
    {

        GridProfile profile = new GridProfile
        {
            HourColumnIndex = 0,
            RatioColumnIndex = 16,
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
            AverageHiddenColumns = new List<int> { 3, 4, 5, 6, 7, 8, 9, 10, 11 }
        };

        profile.Columns = new List<GridColumnProfile>
        {
            new() { Name = "col1",  HeaderText = "Time",         Width = 50,  ReadOnly = true  },
            new() { Name = "col2",  HeaderText = "Inlet Press.", Width = 50,  ReadOnly = false },
            new() { Name = "col3",  HeaderText = "Outlet Press.",Width = 50,  ReadOnly = false },

            new() { Name = "col4",  HeaderText = "Unit1 Status", Width = 50,  ReadOnly = false },
            new() { Name = "col5",  HeaderText = "Unit1 RPM",    Width = 50,  ReadOnly = false },

            new() { Name = "col6",  HeaderText = "Unit2 Status", Width = 50,  ReadOnly = false },
            new() { Name = "col7",  HeaderText = "Unit2 RPM",    Width = 50,  ReadOnly = false },

            new() { Name = "col8",  HeaderText = "Unit3 Status", Width = 50,  ReadOnly = false },
            new() { Name = "col9",  HeaderText = "Unit3 RPM",    Width = 50,  ReadOnly = false },

            new() { Name = "col10", HeaderText = "Unit4 Status", Width = 50,  ReadOnly = false },
            new() { Name = "col11", HeaderText = "Unit4 RPM",    Width = 50,  ReadOnly = false },

            new() { Name = "col12", HeaderText = "Recycle",      Width = 50,  ReadOnly = false },
            new() { Name = "col13", HeaderText = "Flow",         Width = 50,  ReadOnly = false },
            new() { Name = "col14", HeaderText = "Inlet Temp",   Width = 50,  ReadOnly = false },
            new() { Name = "col15", HeaderText = "Outlet Temp",  Width = 50,  ReadOnly = false },
            new() { Name = "col16", HeaderText = "Ambient Temp", Width = 50,  ReadOnly = false },
            new() { Name = "col17", HeaderText = "Ratio",        Width = 50,  ReadOnly = false }

        };

        return profile;
    }
}