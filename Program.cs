using Rah_Negar.Foundation.Application.Authority;
using Rah_Negar.Data;
using Rah_Negar.Infrastructure.Database.Readiness;
using Rah_Negar.Services;
using Rah_Negar.UI.Forms;
using Rah_Negar.UI.Startup;
using Rah_Negar.Infrastructure.ApplicationData;
using Rah_Negar.Utils;

namespace Rah_Negar
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                ApplicationConfiguration.Initialize();
                SQLitePCL.Batteries.Init();

            ApplicationDataPaths paths = ApplicationDataPaths.Default;
            ApplicationDataMigrationService.EnsureReady(paths, AppContext.BaseDirectory);

            string authorityPath = paths.AuthorityStatePath;
            string transitionPath = paths.AuthorityTransitionPath;
            AuthorityStartupResult startup = new AuthorityStartupResolver(new FileAuthorityStateStore(authorityPath))
                .ResolveCanonicalAsync(new FileTransitionStateStore(transitionPath)).GetAwaiter().GetResult();
            if (startup.RoutingBlocked)
            {
                MessageBox.Show(
                    RecoveryOperatorMessage.Persian(startup.Classification, startup.Issues),
                    "راه‌نگار",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (RecoveryRequiredStateStore.IsRequired(SqliteDatabaseHelper.GetDatabasePath()))
            {
                MessageBox.Show(
                    "وضعیت بازیابی دیتابیس قابل اثبات نیست. تا اجرای بازیابی تأییدشده، ورود به عملیات عادی مسدود است.",
                    "Recovery Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (AppInitializationService.IsInitialized())
                Application.Run(new FrmLogin());
            else
                Application.Run(new FrmStartup());
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "startup");
                MessageBox.Show(
                    "داده‌های عملیاتی قابل دسترسی نیستند. مجوز پوشه داده را بررسی کنید یا با مسئول سیستم تماس بگیرید.",
                    "خطا در راه‌اندازی", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
