using Rah_Negar.Services;
using Rah_Negar.UI.Forms;
using Rah_Negar.UI.Startup;

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