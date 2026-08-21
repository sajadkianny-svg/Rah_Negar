using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rah_Negar.UI.Controls;

public class ToggleSwitch : Control
{
    private bool _isOn;

    [Category("Behavior")]
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value)
                return;

            _isOn = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedChanged;

    public ToggleSwitch()
    {
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        Size = new Size(70, 20);
        Font = new Font("tahoma", 7.5F, FontStyle.Bold);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        IsOn = !IsOn;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // رنگ پس‌زمینه کلید
        Color backColor = IsOn
            ? Color.FromArgb(0, 170, 90)    // روشن
            : Color.FromArgb(200, 55, 55);  // خاموش

        using SolidBrush backBrush = new(backColor);
        g.FillRectangle(backBrush, ClientRectangle);

        // بوردر مشکی دور کل کادر
        using Pen borderPen = new(Color.Black, 1);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        // دایره سفید، ۲۰ درصد کوچک‌تر
        int baseSize = Height - 6;
        int knobSize = (int)(baseSize * 0.9);
        int knobPadding = (Height - knobSize) / 2;

        int knobX = IsOn
            ? Width - knobSize - knobPadding
            : knobPadding;

        using SolidBrush knobBrush = new(Color.White);
        g.FillEllipse(knobBrush, knobX, knobPadding, knobSize, knobSize);

        // متن با فاصله از دایره
        string text = IsOn ? "روشن" : "خاموش";

        int textGap = 8;
        Rectangle textRect;

        if (IsOn)
        {
            // دایره سمت راست، متن در سمت چپ
            textRect = new Rectangle(
                2,
                0,
                Width - knobSize - knobPadding - textGap,
                Height);
        }
        else
        {
            // دایره سمت چپ، متن در سمت راست
            int left = knobPadding + knobSize + textGap;

            textRect = new Rectangle(
                left,
                0,
                Width - left - 2,
                Height);
        }

        using StringFormat sf = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        using Brush textBrush = new SolidBrush(Color.White);
        g.DrawString(text, Font, textBrush, textRect, sf);
    }
}

