using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Rah_Negar.Core;

/// <summary>
/// مشخصات ظاهری و رفتاری کلی DataGridView
/// </summary>
public sealed class GridVisualProfile
{
    public bool AllowUserToAddRows { get; set; }
    public bool AllowUserToDeleteRows { get; set; }
    public bool AllowUserToOrderColumns { get; set; }
    public bool AllowUserToResizeColumns { get; set; }
    public bool AllowUserToResizeRows { get; set; }
    public bool MultiSelect { get; set; }
    public bool RowHeadersVisible { get; set; }
    public bool EnableHeadersVisualStyles { get; set; }
    public bool ReadOnly { get; set; }

    public DataGridViewSelectionMode SelectionMode { get; set; }
        = DataGridViewSelectionMode.CellSelect;

    public DataGridViewEditMode EditMode { get; set; }
        = DataGridViewEditMode.EditOnKeystroke;

    public Color HeaderBackColor { get; set; } = Color.LightGray;
    public Color HeaderForeColor { get; set; } = Color.Black;
    public Font? HeaderFont { get; set; }

    public int HeaderHeight { get; set; } = 50;

    public Color GridColor { get; set; } = Color.LightGray;
    public Color SelectionBackColor { get; set; } = Color.FromArgb(135, 206, 250);
    public Color SelectionForeColor { get; set; } = Color.Black;

    public Color AlternateBackColor1 { get; set; } = Color.FromArgb(245, 245, 245);
    public Color AlternateBackColor2 { get; set; } = Color.FromArgb(255, 255, 255);

    public int DataRowCount { get; set; }
    public bool HasAverageRow { get; set; }
    public int AverageRowIndex { get; set; }
}
