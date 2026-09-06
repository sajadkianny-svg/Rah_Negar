using Rah_Negar.Services;
using Rah_Negar.UI.Forms;
using Rah_Negar.UI.Startup;
using Rah_Negar.Foundation.Application.Authority;

namespace Rah_Negar
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            SQLitePCL.Batteries.Init();

            // D2 invariant: resolve canonical authority and any persisted transition before creating a route-capable form.
            string authorityPath = Path.Combine(AppContext.BaseDirectory, "DataFiles", "authority-state.json");
            string transitionPath = Path.Combine(AppContext.BaseDirectory, "DataFiles", "authority-transition.json");
            AuthorityStartupResult startup = new AuthorityStartupResolver(new FileAuthorityStateStore(authorityPath))
                .ResolveCanonicalAsync(new FileTransitionStateStore(transitionPath)).GetAwaiter().GetResult();
if (startup.RoutingBlocked)
{
    MessageBox.Show(
        "وضعیت مرجع داده‌ها نیازمند بازیابی است و برنامه تا رفع این وضعیت قابل اجرا نیست.",
        "راه‌نگار",
        MessageBoxButtons.OK,
        MessageBoxIcon.Warning);
    return;
}

            if (AppInitializationService.IsInitialized())
            {
                Application.Run(new FrmLogin());
            }
            else
            {
                Application.Run(new FrmStartup());
            }
        }
    }
}
