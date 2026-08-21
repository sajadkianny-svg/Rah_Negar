namespace Rah_Negar.Core;

public static class AppSession
{
    public static bool IsLoggedIn { get; private set; }

    public static void Login()
    {
        IsLoggedIn = true;
    }

    public static void Logout()
    {
        IsLoggedIn = false;
    }
}
