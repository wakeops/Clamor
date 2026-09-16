using System.Diagnostics;

namespace Clamor.App.Services;

public static class VbCableLauncher
{
    private const string DownloadUrl = "https://vb-audio.com/Cable/";

    public static void OpenDownloadPage()
    {
        Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute = true });
    }
}
