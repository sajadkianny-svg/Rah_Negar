using Rah_Negar.Services.UI;
using System.Windows.Forms;

namespace Rah_Negar.Tests.UI;

public sealed class DialogLocalizationTests
{
    [Fact]
    public void Native_confirmation_buttons_use_persian_captions_without_changing_button_sets()
    {
        Assert.Equal("بله", UiMessageService.GetLocalizedButtonCaptionForTesting(6, MessageBoxButtons.YesNo));
        Assert.Equal("خیر", UiMessageService.GetLocalizedButtonCaptionForTesting(7, MessageBoxButtons.YesNo));
        Assert.Equal("تأیید", UiMessageService.GetLocalizedButtonCaptionForTesting(1, MessageBoxButtons.OK));
        Assert.Equal("انصراف", UiMessageService.GetLocalizedButtonCaptionForTesting(2, MessageBoxButtons.OKCancel));
    }

    [Fact]
    public void Backup_restore_and_recovery_dialog_contracts_are_persian_and_fail_closed()
    {
        string root = RepositoryRoot();
        string settings = File.ReadAllText(Path.Combine(root, "UI", "Forms", "FrmSettings.cs"));
        string program = File.ReadAllText(Path.Combine(root, "Program.cs"));
        string service = File.ReadAllText(Path.Combine(root, "Services", "UI", "UiMessageService.cs"));

        Assert.Contains("نسخه پشتیبان ایجاد می‌شود. ادامه می‌دهید؟", settings, StringComparison.Ordinal);
        Assert.Contains("بازیابی نسخه پشتیبان، داده‌های جداشده فعلی را جایگزین می‌کند. ادامه می‌دهید؟", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"qualification", settings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"ManagementCredential", settings, StringComparison.Ordinal);
        Assert.Contains("MessageBoxButtons.YesNo", settings, StringComparison.Ordinal);
        Assert.Contains("!= DialogResult.Yes", settings, StringComparison.Ordinal);
        Assert.Contains("return result == DialogResult.Yes;", service, StringComparison.Ordinal);
        Assert.Contains("return result == DialogResult.OK;", service, StringComparison.Ordinal);
        Assert.Contains("بازیابی مورد نیاز", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Recovery Required", program, StringComparison.Ordinal);
        Assert.Contains("UiMessageService.ShowMessageBox", program, StringComparison.Ordinal);
    }

    private static string RepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
