using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;

namespace Rah_Negar.Core;

/// <summary>
/// پالت کامل رنگی برنامه را نگهداری می‌کند.
/// هر تم شامل رنگ‌های هماهنگ برای فرم، پنل‌ها، دکمه‌ها و گریدها است.
/// </summary>
public sealed class AppThemePalette
{
    public string Name { get; init; } = string.Empty;

    public Color FormBackColor { get; init; }
    public Color HeaderBackColor { get; init; }

    public Color ContentBackColor { get; init; }
    public Color CardBackColor { get; init; }
    public Color DividerBackColor { get; init; }

    public Color PrimaryButtonBackColor { get; init; }
    public Color PrimaryButtonHoverColor { get; init; }
    public Color PrimaryButtonDownColor { get; init; }

    public Color NavigationActiveBackColor { get; init; }
    public Color NavigationInactiveBackColor { get; init; }
    public Color NavigationHoverBackColor { get; init; }

    public Color GridHeaderBackColor { get; init; }
    public Color GridFixedCellBackColor { get; init; }
    public Color GridCellBackColor { get; init; }
    public Color GridLineColor { get; init; }

    public Color TextPrimaryColor { get; init; }
    public Color TextSecondaryColor { get; init; }
    public Color TextOnAccentColor { get; init; }
}


