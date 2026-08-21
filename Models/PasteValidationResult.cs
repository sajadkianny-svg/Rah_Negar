using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Rah_Negar.Models;

public sealed class PasteValidationResult
{
    public bool IsValid { get; set; }
    public int RowIndex { get; set; } = -1;
    public int ColumnIndex { get; set; } = -1;
    public string Message { get; set; } = string.Empty;
}
