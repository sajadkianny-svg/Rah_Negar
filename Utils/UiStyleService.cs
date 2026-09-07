using Rah_Negar.Core;

namespace Rah_Negar.Utils;

/// <summary>
/// Shared presentation conventions for the operator-facing WinForms surface.
/// This service deliberately changes only appearance and control affordances;
/// it does not change control order, bindings, or operational terminology.
/// </summary>
public static class UiStyleService
{
    public const string FontFamily = "Segoe UI";
    public const float NormalFontSize = 9f;
    public const float SectionFontSize = 9.5f;
    public const float FormHeadingFontSize = 13f;
    public const int StandardControlHeight = 28;
    public const int StandardButtonHeight = 32;
    public const int StandardSpacing = 8;
    public const int StandardPadding = 10;

    public static Font CreateFont(float size = NormalFontSize, FontStyle style = FontStyle.Regular) =>
        new(FontFamily, size, style, GraphicsUnit.Point);

    public static void ApplyFormConventions(Form form, AppThemePalette palette)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(palette);

        form.Font = CreateFont();
        form.BackColor = palette.FormBackColor;
        form.ForeColor = palette.TextPrimaryColor;
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeftLayout = true;

        ApplyControlConventions(form, palette);
    }

    public static void ApplyControlConventions(Control root, AppThemePalette palette)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(palette);

        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case Button button:
                    ApplyButtonConventions(button, palette);
                    break;

                case TextBox textBox:
                    textBox.Font = CreateFont();
                    textBox.MinimumSize = new Size(textBox.MinimumSize.Width,
                        Math.Max(textBox.MinimumSize.Height, UiScaleService.Scale(control, StandardControlHeight)));
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    textBox.RightToLeft = RightToLeft.Yes;
                    break;

                case ComboBox comboBox:
                    comboBox.Font = CreateFont();
                    comboBox.MinimumSize = new Size(comboBox.MinimumSize.Width,
                        Math.Max(comboBox.MinimumSize.Height, UiScaleService.Scale(control, StandardControlHeight)));
                    comboBox.IntegralHeight = false;
                    comboBox.RightToLeft = RightToLeft.Yes;
                    break;

                case DateTimePicker dateTimePicker:
                    dateTimePicker.Font = CreateFont();
                    dateTimePicker.MinimumSize = new Size(dateTimePicker.MinimumSize.Width,
                        Math.Max(dateTimePicker.MinimumSize.Height, UiScaleService.Scale(control, StandardControlHeight)));
                    dateTimePicker.RightToLeft = RightToLeft.Yes;
                    break;

                case GroupBox groupBox:
                    groupBox.Font = CreateFont(SectionFontSize, FontStyle.Bold);
                    groupBox.ForeColor = palette.TextPrimaryColor;
                    break;

                case TabControl tabControl:
                    tabControl.Font = CreateFont();
                    tabControl.Padding = new Point(UiScaleService.Scale(control, 12),
                        UiScaleService.Scale(control, 6));
                    break;

                case DataGridView grid:
                    ApplyGridConventions(grid, palette);
                    break;
            }

            if (control.HasChildren)
                ApplyControlConventions(control, palette);
        }
    }

    public static void ApplyButtonConventions(Button button, AppThemePalette palette)
    {
        ArgumentNullException.ThrowIfNull(button);
        ArgumentNullException.ThrowIfNull(palette);

        button.Font = CreateFont();
        button.MinimumSize = new Size(button.MinimumSize.Width,
            Math.Max(button.MinimumSize.Height, UiScaleService.Scale(button, StandardButtonHeight)));
        button.Padding = new Padding(UiScaleService.Scale(button, 10),
            UiScaleService.Scale(button, 3),
            UiScaleService.Scale(button, 10),
            UiScaleService.Scale(button, 3));
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = palette.DividerBackColor;
        button.FlatAppearance.MouseOverBackColor = palette.NavigationHoverBackColor;
        button.FlatAppearance.MouseDownBackColor = palette.PrimaryButtonDownColor;
        button.Cursor = button.Enabled ? Cursors.Hand : Cursors.Default;
    }

    public static void ApplyGridConventions(DataGridView grid, AppThemePalette palette)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(palette);

        // Keep the logical operational column order unchanged when the parent
        // form is RTL. Text direction is handled by cell alignment instead.
        grid.RightToLeft = RightToLeft.No;
        grid.Font = CreateFont(8.5f);
        grid.DefaultCellStyle.Font = CreateFont(8.5f);
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = palette.ContentBackColor;
        grid.GridColor = palette.GridLineColor;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersDefaultCellStyle.BackColor = palette.GridHeaderBackColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = palette.TextOnAccentColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = palette.GridHeaderBackColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = palette.TextOnAccentColor;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.DefaultCellStyle.BackColor = palette.GridCellBackColor;
        grid.DefaultCellStyle.ForeColor = palette.TextPrimaryColor;
        grid.DefaultCellStyle.SelectionBackColor = palette.NavigationHoverBackColor;
        grid.DefaultCellStyle.SelectionForeColor = palette.TextPrimaryColor;
        grid.RowTemplate.Height = UiScaleService.Scale(grid, 26);
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
    }
}
