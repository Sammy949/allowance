using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Allowance;

/// <summary>Classic Windows toasts with an AppUserModelID under HKCU, which works for unpackaged apps.</summary>
internal static class Notifier
{
    private const string Aumid = "Allowance.App";
    private static ToastNotifier? _notifier;

    public static void Register()
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{Aumid}"))
            {
                key.SetValue("DisplayName", "Allowance");
                key.SetValue("IconUri", Path.Combine(AppContext.BaseDirectory, "Assets", "Allowance.png"));
            }

            SetCurrentProcessExplicitAppUserModelID(Aumid);
            _notifier = ToastNotificationManager.CreateToastNotifier(Aumid);
        }
        catch (Exception e)
        {
            Log.Write($"notifications unavailable: {e.Message}");
        }
    }

    public static void Show(string title, string body)
    {
        Log.Write($"notify: {title} | {body}");
        if (_notifier is null)
        {
            return;
        }

        try
        {
            var xml = new XmlDocument();
            xml.LoadXml($"<toast><visual><binding template=\"ToastGeneric\"><text>{SecurityElement.Escape(title)}</text><text>{SecurityElement.Escape(body)}</text></binding></visual></toast>");
            _notifier.Show(new ToastNotification(xml));
        }
        catch (Exception e)
        {
            Log.Write($"notification failed: {e.Message}");
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
