using System.Runtime.InteropServices;
using ExcelFormAssistant.Services;
using static ExcelFormAssistant.Services.NativeMethods;

namespace ExcelFormAssistant.Tests;

/// <summary>Ces tests écrivent dans le vrai presse-papiers Windows.</summary>
[Collection("Presse-papiers")]
public sealed class ClipboardServiceTests
{
    [Theory]
    [InlineData("0550123456")]
    [InlineData("15/05/1993")]
    [InlineData("Tizi Ouzou – é à ç")]
    public void SetText_CopiesTextExactly(string text)
    {
        using var clipboard = new ClipboardService();

        Assert.True(clipboard.SetText(text));

        Assert.Equal(text, ReadClipboardText());
    }

    [Fact]
    public void SetText_ExcludesValueFromWinVHistoryAndCloud()
    {
        using var clipboard = new ClipboardService();

        Assert.True(clipboard.SetText("BENALI"));

        Assert.True(IsClipboardFormatAvailable(RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing")));
        Assert.Equal(0, ReadClipboardDword("CanIncludeInClipboardHistory"));
        Assert.Equal(0, ReadClipboardDword("CanUploadToCloudClipboard"));
    }

    private static string? ReadClipboardText() => WithOpenClipboard(() =>
    {
        var handle = GetClipboardData(CF_UNICODETEXT);
        var pointer = GlobalLock(handle);
        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            GlobalUnlock(handle);
        }
    });

    private static int ReadClipboardDword(string format) => WithOpenClipboard(() =>
    {
        var handle = GetClipboardData(RegisterClipboardFormat(format));
        Assert.NotEqual(IntPtr.Zero, handle);
        var pointer = GlobalLock(handle);
        try
        {
            return Marshal.ReadInt32(pointer);
        }
        finally
        {
            GlobalUnlock(handle);
        }
    });

    private static T WithOpenClipboard<T>(Func<T> read)
    {
        for (int attempt = 0; attempt < 20 && !OpenClipboard(IntPtr.Zero); attempt++)
            Thread.Sleep(20);
        try
        {
            return read();
        }
        finally
        {
            CloseClipboard();
        }
    }
}
