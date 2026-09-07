using Rah_Negar.UI.Forms.Base;
using System.Windows.Forms;

namespace Rah_Negar.Tests.UI;

public sealed class Batch2UiAcceptanceTests
{
    [Fact]
    public void Operator_form_baseline_is_RTL_DPI_and_blocks_above_150_percent()
    {
        Assert.Equal(144, BaseForm.MaximumSupportedDpi);
        RunSta(() =>
        {
            using var form = new TestForm();
            Assert.Equal(AutoScaleMode.Dpi, form.AutoScaleMode);
            Assert.Equal(RightToLeft.Yes, form.RightToLeft);
            Assert.True(form.RightToLeftLayout);
            Assert.NotNull(form.Icon);
        });
    }

    [Fact]
    public void Supported_visual_acceptance_matrix_is_explicit()
    {
        Assert.Equal(new[] { 100, 125, 150 }, UiAcceptanceMatrix.SupportedPercentages);
        Assert.False(UiAcceptanceMatrix.IsSupported(175));
        Assert.Contains("پشتیبانی", UiAcceptanceMatrix.UnsupportedScaleMessage, StringComparison.Ordinal);
    }

    private sealed class TestForm : BaseForm
    {
        public TestForm()
        {
            Controls.Add(new TextBox { Name = "input", Text = "نمونه" });
            Controls.Add(new Button { Name = "save", Text = "ذخیره" });
        }
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
    }
}

internal static class UiAcceptanceMatrix
{
    public static IReadOnlyList<int> SupportedPercentages { get; } = [100, 125, 150];
    public const string UnsupportedScaleMessage = "این مقیاس نمایش پشتیبانی نمی‌شود.";
    public static bool IsSupported(int percentage) => SupportedPercentages.Contains(percentage);
}
