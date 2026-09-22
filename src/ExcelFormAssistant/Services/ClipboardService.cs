using System.Runtime.InteropServices;
using static ExcelFormAssistant.Services.NativeMethods;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Copie du texte dans le presse-papiers via l'API Windows, en l'excluant de
/// l'historique Win+V et de la synchronisation cloud.
/// </summary>
public sealed class ClipboardService : IDisposable
{
    // Formats reconnus par Windows 10/11 (et par la plupart des gestionnaires de presse-papiers).
    private static readonly uint ExcludeFromMonitoring = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint CanIncludeInHistory = RegisterClipboardFormat("CanIncludeInClipboardHistory");
    private static readonly uint CanUploadToCloud = RegisterClipboardFormat("CanUploadToCloudClipboard");

    // Fenêtre invisible propriétaire du presse-papiers : avec OpenClipboard(NULL), SetClipboardData échoue.
    private readonly IntPtr _owner = CreateWindowEx(0, "STATIC", "ExcelFormAssistant.Clipboard", 0, 0, 0, 0, 0,
        HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

    /// <summary>Copie <paramref name="text"/> tel quel. Renvoie false si le presse-papiers est resté occupé.</summary>
    public bool SetText(string text)
    {
        // Un autre programme peut tenir le presse-papiers quelques millisecondes : on réessaie.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(_owner))
            {
                try
                {
                    EmptyClipboard();
                    return SetData(CF_UNICODETEXT, System.Text.Encoding.Unicode.GetBytes(text + "\0"))
                        && SetData(ExcludeFromMonitoring, BitConverter.GetBytes(0))
                        && SetData(CanIncludeInHistory, BitConverter.GetBytes(0))
                        && SetData(CanUploadToCloud, BitConverter.GetBytes(0));
                }
                finally
                {
                    CloseClipboard();
                }
            }

            Thread.Sleep(20);
        }

        return false;
    }

    private static bool SetData(uint format, byte[] data)
    {
        var handle = GlobalAlloc(GMEM_MOVEABLE, (nuint)data.Length);
        if (handle == IntPtr.Zero)
            return false;

        var pointer = GlobalLock(handle);
        Marshal.Copy(data, 0, pointer, data.Length);
        GlobalUnlock(handle);

        // En cas de succès, Windows devient propriétaire de la mémoire.
        if (SetClipboardData(format, handle) != IntPtr.Zero)
            return true;

        GlobalFree(handle);
        return false;
    }

    public void Dispose() => DestroyWindow(_owner);
}
