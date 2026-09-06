using Rah_Negar.Foundation.Application.Authority;
using Rah_Negar.Services;
using Rah_Negar.UI.Forms;
using Rah_Negar.UI.Startup;

namespace Rah_Negar
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            SQLitePCL.Batteries.Init();

            string authorityPath = Path.Combine(AppContext.BaseDirectory, "DataFiles", "authority-state.json");
            string transitionPath = Path.Combine(AppContext.BaseDirectory, "DataFiles", "authority-transition.json");
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

            if (AppInitializationService.IsInitialized())
                Application.Run(new FrmLogin());
            else
                Application.Run(new FrmStartup());
        }
    }
}
