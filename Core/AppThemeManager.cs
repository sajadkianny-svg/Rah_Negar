using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;

namespace Rah_Negar.Core;

/// <summary>
/// مدیریت رنگ تم برنامه و ذخیره/بازیابی آن.
/// </summary>
public static class AppThemeManager
{
    private static readonly Random _random = new();

    //1 Create Themes=============================================================
    /// <summary>
    /// پالت رنگی فعال برنامه.
    /// </summary>
    public static AppThemePalette CurrentPalette { get; private set; } = CreateIndustrialBlueTheme();

    /// <summary>
    /// تم آبی صنعتی.
    /// </summary>
    private static AppThemePalette CreateIndustrialBlueTheme()
    {
        return new AppThemePalette
        {
            Name = "Industrial Blue",

            FormBackColor = Color.FromArgb(244, 247, 250),
            HeaderBackColor = Color.FromArgb(79, 105, 132),

            ContentBackColor = Color.FromArgb(244, 247, 250),
            CardBackColor = Color.FromArgb(252, 253, 254),
            DividerBackColor = Color.FromArgb(215, 222, 230),

            PrimaryButtonBackColor = Color.FromArgb(88, 126, 160),
            PrimaryButtonHoverColor = Color.FromArgb(104, 142, 176),
            PrimaryButtonDownColor = Color.FromArgb(67, 98, 128),

            NavigationActiveBackColor = Color.FromArgb(88, 126, 160),
            NavigationInactiveBackColor = Color.FromArgb(252, 253, 254),
            NavigationHoverBackColor = Color.FromArgb(226, 235, 243),

            GridHeaderBackColor = Color.FromArgb(88, 126, 160),
            GridFixedCellBackColor = Color.FromArgb(231, 237, 243),
            GridCellBackColor = Color.FromArgb(252, 253, 254),
            GridLineColor = Color.FromArgb(214, 222, 230),

            TextPrimaryColor = Color.FromArgb(35, 43, 52),
            TextSecondaryColor = Color.FromArgb(92, 105, 118),
            TextOnAccentColor = Color.White
        };
    }

    /// <summary>
    /// تم خاکستری-فیروزه‌ای صنعتی.
    /// </summary>
    private static AppThemePalette CreateGraphiteTealTheme()
    {
        return new AppThemePalette
        {
            Name = "Graphite Teal",

            FormBackColor = Color.FromArgb(244, 247, 247),
            HeaderBackColor = Color.FromArgb(70, 95, 96),

            ContentBackColor = Color.FromArgb(244, 247, 247),
            CardBackColor = Color.FromArgb(252, 253, 253),
            DividerBackColor = Color.FromArgb(214, 224, 224),

            PrimaryButtonBackColor = Color.FromArgb(76, 129, 128),
            PrimaryButtonHoverColor = Color.FromArgb(91, 145, 144),
            PrimaryButtonDownColor = Color.FromArgb(55, 102, 101),

            NavigationActiveBackColor = Color.FromArgb(76, 129, 128),
            NavigationInactiveBackColor = Color.FromArgb(252, 253, 253),
            NavigationHoverBackColor = Color.FromArgb(225, 238, 238),

            GridHeaderBackColor = Color.FromArgb(76, 129, 128),
            GridFixedCellBackColor = Color.FromArgb(229, 239, 239),
            GridCellBackColor = Color.FromArgb(252, 253, 253),
            GridLineColor = Color.FromArgb(212, 224, 224),

            TextPrimaryColor = Color.FromArgb(35, 45, 45),
            TextSecondaryColor = Color.FromArgb(90, 105, 105),
            TextOnAccentColor = Color.White
        };
    }


    /// <summary>
    /// تم زیتونی-طلایی ملایم.
    /// </summary>
    private static AppThemePalette CreateOliveGoldTheme()
    {
        return new AppThemePalette
        {
            Name = "Olive Gold",

            FormBackColor = Color.FromArgb(246, 247, 241),
            HeaderBackColor = Color.FromArgb(101, 111, 76),

            ContentBackColor = Color.FromArgb(246, 247, 241),
            CardBackColor = Color.FromArgb(253, 253, 249),
            DividerBackColor = Color.FromArgb(222, 224, 210),

            PrimaryButtonBackColor = Color.FromArgb(126, 137, 90),
            PrimaryButtonHoverColor = Color.FromArgb(143, 154, 105),
            PrimaryButtonDownColor = Color.FromArgb(94, 104, 68),

            NavigationActiveBackColor = Color.FromArgb(126, 137, 90),
            NavigationInactiveBackColor = Color.FromArgb(253, 253, 249),
            NavigationHoverBackColor = Color.FromArgb(235, 238, 222),

            GridHeaderBackColor = Color.FromArgb(126, 137, 90),
            GridFixedCellBackColor = Color.FromArgb(235, 238, 222),
            GridCellBackColor = Color.FromArgb(253, 253, 249),
            GridLineColor = Color.FromArgb(221, 224, 210),

            TextPrimaryColor = Color.FromArgb(43, 46, 36),
            TextSecondaryColor = Color.FromArgb(100, 105, 85),
            TextOnAccentColor = Color.White
        };
    }


    /// <summary>
    /// تم Terracotta Stone
    /// گرم، صنعتی، آرام و غیرتکراری
    /// </summary>
    private static AppThemePalette CreateTerracottaStoneTheme()
    {
        return new AppThemePalette
        {
            Name = "Terracotta Stone",

            FormBackColor = Color.FromArgb(247, 244, 241),
            HeaderBackColor = Color.FromArgb(126, 91, 73),

            ContentBackColor = Color.FromArgb(247, 244, 241),
            CardBackColor = Color.FromArgb(253, 251, 249),
            DividerBackColor = Color.FromArgb(224, 213, 207),

            PrimaryButtonBackColor = Color.FromArgb(158, 103, 78),
            PrimaryButtonHoverColor = Color.FromArgb(176, 119, 92),
            PrimaryButtonDownColor = Color.FromArgb(123, 78, 59),

            NavigationActiveBackColor = Color.FromArgb(158, 103, 78),
            NavigationInactiveBackColor = Color.FromArgb(253, 251, 249),
            NavigationHoverBackColor = Color.FromArgb(239, 229, 223),

            GridHeaderBackColor = Color.FromArgb(158, 103, 78),
            GridFixedCellBackColor = Color.FromArgb(238, 228, 222),
            GridCellBackColor = Color.FromArgb(253, 251, 249),
            GridLineColor = Color.FromArgb(222, 210, 203),

            TextPrimaryColor = Color.FromArgb(45, 38, 35),
            TextSecondaryColor = Color.FromArgb(105, 92, 86),
            TextOnAccentColor = Color.White
        };
    }


    /// <summary>
    /// تم Indigo Violet
    /// مدرن، متفاوت، خارج از فضای سبز/کرم/آبی
    /// </summary>
    private static AppThemePalette CreateIndigoVioletTheme()
    {
        return new AppThemePalette
        {
            Name = "Indigo Violet",

            FormBackColor = Color.FromArgb(245, 245, 250),
            HeaderBackColor = Color.FromArgb(82, 78, 124),

            ContentBackColor = Color.FromArgb(245, 245, 250),
            CardBackColor = Color.FromArgb(253, 253, 255),
            DividerBackColor = Color.FromArgb(219, 220, 232),

            PrimaryButtonBackColor = Color.FromArgb(103, 96, 154),
            PrimaryButtonHoverColor = Color.FromArgb(119, 112, 170),
            PrimaryButtonDownColor = Color.FromArgb(78, 72, 126),

            NavigationActiveBackColor = Color.FromArgb(103, 96, 154),
            NavigationInactiveBackColor = Color.FromArgb(253, 253, 255),
            NavigationHoverBackColor = Color.FromArgb(232, 232, 244),

            GridHeaderBackColor = Color.FromArgb(103, 96, 154),
            GridFixedCellBackColor = Color.FromArgb(234, 234, 245),
            GridCellBackColor = Color.FromArgb(253, 253, 255),
            GridLineColor = Color.FromArgb(218, 219, 232),

            TextPrimaryColor = Color.FromArgb(38, 38, 48),
            TextSecondaryColor = Color.FromArgb(95, 95, 115),
            TextOnAccentColor = Color.White
        };
    }


    /// <summary>
    /// تم Industrial Red
    /// قرمز صنعتی کنترل‌شده (Accent محور)
    /// </summary>
    private static AppThemePalette CreateIndustrialRedTheme()
    {
        return new AppThemePalette
        {
            Name = "Industrial Red",

            FormBackColor = Color.FromArgb(246, 246, 246),
            HeaderBackColor = Color.FromArgb(124, 66, 66),

            ContentBackColor = Color.FromArgb(246, 246, 246),
            CardBackColor = Color.FromArgb(253, 253, 253),
            DividerBackColor = Color.FromArgb(222, 218, 218),

            PrimaryButtonBackColor = Color.FromArgb(154, 76, 76),
            PrimaryButtonHoverColor = Color.FromArgb(174, 92, 92),
            PrimaryButtonDownColor = Color.FromArgb(116, 52, 52),

            NavigationActiveBackColor = Color.FromArgb(154, 76, 76),
            NavigationInactiveBackColor = Color.FromArgb(253, 253, 253),
            NavigationHoverBackColor = Color.FromArgb(239, 229, 229),

            GridHeaderBackColor = Color.FromArgb(154, 76, 76),
            GridFixedCellBackColor = Color.FromArgb(239, 229, 229),
            GridCellBackColor = Color.FromArgb(253, 253, 253),
            GridLineColor = Color.FromArgb(222, 218, 218),

            TextPrimaryColor = Color.FromArgb(35, 35, 35),
            TextSecondaryColor = Color.FromArgb(92, 92, 92),
            TextOnAccentColor = Color.White
        };
    }



    /// <summary>
    /// تم کلاسیک خنثی
    /// سفید شیری + طیف خاکستری گرم
    /// مینیمال، آرام و مناسب کار طولانی
    /// </summary>
    private static AppThemePalette CreateClassicNeutralTheme()
    {
        return new AppThemePalette
        {
            Name = "Classic Neutral",

            // بک‌گراندها (سفید شیری)
            FormBackColor = Color.FromArgb(246, 245, 242),
            ContentBackColor = Color.FromArgb(246, 245, 242),
            CardBackColor = Color.FromArgb(252, 251, 248),

            // هدر (خاکستری گرم مات)
            HeaderBackColor = Color.FromArgb(165, 162, 155),

            // خطوط
            DividerBackColor = Color.FromArgb(220, 218, 212),

            // دکمه‌ها (خاکستری متوسط با ته‌گرم)
            PrimaryButtonBackColor = Color.FromArgb(140, 138, 132),
            PrimaryButtonHoverColor = Color.FromArgb(155, 152, 145),
            PrimaryButtonDownColor = Color.FromArgb(110, 108, 102),

            // ناوبری
            NavigationActiveBackColor = Color.FromArgb(140, 138, 132),
            NavigationInactiveBackColor = Color.FromArgb(252, 251, 248),
            NavigationHoverBackColor = Color.FromArgb(232, 230, 224),

            // گرید
            GridHeaderBackColor = Color.FromArgb(140, 138, 132),
            GridFixedCellBackColor = Color.FromArgb(232, 230, 224),
            GridCellBackColor = Color.FromArgb(252, 251, 248),
            GridLineColor = Color.FromArgb(218, 216, 210),

            // متن
            TextPrimaryColor = Color.FromArgb(40, 40, 38),
            TextSecondaryColor = Color.FromArgb(95, 93, 88),
            TextOnAccentColor = Color.White
        };
    }

    /// <summary>
    /// تم کلاسیک با Accent ملایم بژ/برنز
    /// خنثی، حرفه‌ای، مناسب استفاده طولانی
    /// </summary>
    private static AppThemePalette CreateClassicSoftAccentTheme()
    {
        return new AppThemePalette
        {
            Name = "Classic Soft Accent",

            // بک‌گراند (سفید شیری)
            FormBackColor = Color.FromArgb(246, 245, 242),
            ContentBackColor = Color.FromArgb(246, 245, 242),
            CardBackColor = Color.FromArgb(252, 251, 248),

            // هدر (خاکستری گرم)
            HeaderBackColor = Color.FromArgb(160, 155, 145),

            // خطوط
            DividerBackColor = Color.FromArgb(222, 220, 214),

            // 🎯 Accent (بژ/برنز خیلی ملایم)
            PrimaryButtonBackColor = Color.FromArgb(168, 150, 120),
            PrimaryButtonHoverColor = Color.FromArgb(182, 165, 135),
            PrimaryButtonDownColor = Color.FromArgb(140, 125, 100),

            // ناوبری
            NavigationActiveBackColor = Color.FromArgb(168, 150, 120),
            NavigationInactiveBackColor = Color.FromArgb(252, 251, 248),
            NavigationHoverBackColor = Color.FromArgb(236, 230, 220),

            // گرید
            GridHeaderBackColor = Color.FromArgb(150, 145, 135),
            GridFixedCellBackColor = Color.FromArgb(235, 230, 220),
            GridCellBackColor = Color.FromArgb(252, 251, 248),
            GridLineColor = Color.FromArgb(215, 210, 200),

            // متن
            TextPrimaryColor = Color.FromArgb(38, 38, 36),
            TextSecondaryColor = Color.FromArgb(100, 95, 88),
            TextOnAccentColor = Color.White
        };
    }



    //2 Public Methods Theme Manager=============================================
    /// <summary>
    /// تم را بر اساس شماره انتخاب می‌کند.
    /// </summary>
    public static void LoadThemeByIndex(int themeIndex)
    {
        CurrentPalette = themeIndex switch
        {
            0 => CreateIndustrialBlueTheme(),
            1 => CreateGraphiteTealTheme(),
            2 => CreateOliveGoldTheme(),
            3 => CreateTerracottaStoneTheme(),
            4 => CreateIndigoVioletTheme(),
            5 => CreateIndustrialRedTheme(),
            6 => CreateClassicNeutralTheme(),
            7 => CreateClassicSoftAccentTheme(),
            _ => CreateIndustrialBlueTheme()

        };

        CurrentAccentColor = CurrentPalette.PrimaryButtonBackColor;
    }

    /// <summary>
    /// تم بعدی را فعال می‌کند.
    /// </summary>
    public static int LoadNextTheme(int currentThemeIndex)
    {
        int nextIndex = currentThemeIndex + 1;

        if (nextIndex > 7)
            nextIndex = 0;

        LoadThemeByIndex(nextIndex);

        return nextIndex;
    }

    /// <summary>
    /// اعمال رنگ روی پنل.
    /// </summary>
    public static void ApplyToPanel(Panel panel, Color backColor)
    {
        panel.BackColor = backColor;
    }

    /// <summary>
    /// اعمال تم روی دکمه اصلی.
    /// </summary>
    public static void ApplyToPrimaryButton(Button button)
    {
        button.BackColor = CurrentPalette.PrimaryButtonBackColor;
        button.ForeColor = CurrentPalette.TextOnAccentColor;

        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = CurrentPalette.PrimaryButtonHoverColor;
        button.FlatAppearance.MouseDownBackColor = CurrentPalette.PrimaryButtonDownColor;
    }

    /// <summary>
    /// اعمال تم روی دکمه ناوبری.
    /// </summary>
    public static void ApplyToNavigationButton(Button button, bool isActive)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;

        button.BackColor = isActive
            ? CurrentPalette.NavigationActiveBackColor
            : CurrentPalette.NavigationInactiveBackColor;

        button.ForeColor = isActive
            ? CurrentPalette.TextOnAccentColor
            : CurrentPalette.TextPrimaryColor;

        button.FlatAppearance.MouseOverBackColor = CurrentPalette.NavigationHoverBackColor;
        button.FlatAppearance.MouseDownBackColor = CurrentPalette.PrimaryButtonDownColor;
    }

    /// <summary>
    /// اعمال تم روی DataGridView گزارش.
    /// </summary>
    public static void ApplyToReportGrid(DataGridView dgv)
    {
        dgv.BackgroundColor = CurrentPalette.CardBackColor;
        dgv.GridColor = CurrentPalette.GridLineColor;

        dgv.EnableHeadersVisualStyles = false;

        dgv.ColumnHeadersDefaultCellStyle.BackColor = CurrentPalette.GridHeaderBackColor;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = CurrentPalette.TextOnAccentColor;
        dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = CurrentPalette.GridHeaderBackColor;
        dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = CurrentPalette.TextOnAccentColor;

        dgv.DefaultCellStyle.BackColor = CurrentPalette.GridCellBackColor;
        dgv.DefaultCellStyle.ForeColor = CurrentPalette.TextPrimaryColor;
        dgv.DefaultCellStyle.SelectionBackColor = CurrentPalette.GridCellBackColor;
        dgv.DefaultCellStyle.SelectionForeColor = CurrentPalette.TextPrimaryColor;
    }

    /// <summary>
    /// اعمال رنگ مخصوص سلول‌های ثابت گرید.
    /// </summary>
    public static void ApplyFixedCellStyle(DataGridViewCellStyle style)
    {
        style.BackColor = CurrentPalette.GridFixedCellBackColor;
        style.ForeColor = CurrentPalette.TextPrimaryColor;
        style.SelectionBackColor = CurrentPalette.GridFixedCellBackColor;
        style.SelectionForeColor = CurrentPalette.TextPrimaryColor;
    }

    //============================================================
   
    public static Color CurrentAccentColor { get; private set; }

    public static void Load(Color savedOrDefault)
    {
        CurrentAccentColor = savedOrDefault;
    }

    public static Color GenerateRandomAccentColor()
    {
        CurrentAccentColor = Color.FromArgb(
            _random.Next(40, 210),
            _random.Next(40, 210),
            _random.Next(40, 210));

        return CurrentAccentColor;
    }

    public static Color ResetToProfileDefault(IStationProfile profile)
    {
        CurrentAccentColor = profile.DefaultAccentColor;
        return CurrentAccentColor;
    }

    public static void ApplyToPanels(params Panel[] panels)
    {
        foreach (Panel panel in panels)
        {
            if (panel != null)
                panel.BackColor = CurrentAccentColor;
        }
    }

    public static Color GetLightAccent()
    {
        return ControlPaint.Light(CurrentAccentColor);
    }

    public static Color GetDarkAccent()
    {
        return ControlPaint.Dark(CurrentAccentColor);
    }
}
