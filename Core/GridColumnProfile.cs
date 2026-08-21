using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Core;

/// <summary>
/// مشخصات یک ستون در DataGridView
/// </summary>
public sealed class GridColumnProfile
{
    /// <summary>
    /// نام داخلی ستون
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// عنوان نمایشی ستون
    /// </summary>
    public string HeaderText { get; set; } = string.Empty;

    /// <summary>
    /// عرض ستون
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// فقط‌خواندنی بودن ستون
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// تراز متن ستون
    /// </summary>
    public DataGridViewContentAlignment Alignment { get; set; }
        = DataGridViewContentAlignment.MiddleCenter;
}
