using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MagickCrop.Helpers;

/// <summary>
/// Provides Windows Share sheet integration for WPF desktop apps
/// using the IDataTransferManagerInterop COM interface.
/// </summary>
public static class ShareHelper
{
    private static readonly Guid _dtm_iid =
        new(0xa5caee9b, 0x8708, 0x49d1, 0x8d, 0x36, 0x67, 0xd2, 0x5a, 0x8d, 0xa0, 0x0c);

    /// <summary>
    /// Shows the Windows Share UI to share an image file.
    /// </summary>
    /// <param name="window">The WPF window that owns the share UI.</param>
    /// <param name="imageFilePath">Path to the image file to share.</param>
    /// <param name="title">Title displayed in the share sheet.</param>
    /// <param name="displayFileName">Friendly filename (without extension) shown to the share target. When provided the image is copied to a temp file with this name.</param>
    public static void ShareImageFile(Window window, string imageFilePath, string title, string? displayFileName = null)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;

        IDataTransferManagerInterop interop =
            DataTransferManager.As<IDataTransferManagerInterop>();

        nint result = interop.GetForWindow(hwnd, _dtm_iid);
        DataTransferManager dataTransferManager =
            WinRT.MarshalInterface<DataTransferManager>.FromAbi(result);

        // When a friendly display name is provided, copy to a temp file so the
        // share target receives a file with the original name instead of the
        // internal cache filename.
        string shareFilePath = imageFilePath;
        if (!string.IsNullOrEmpty(displayFileName))
        {
            try
            {
                string extension = Path.GetExtension(imageFilePath);
                string tempDir = Path.Combine(Path.GetTempPath(), "MagickCrop_Share");
                Directory.CreateDirectory(tempDir);
                shareFilePath = Path.Combine(tempDir, displayFileName + extension);
                File.Copy(imageFilePath, shareFilePath, overwrite: true);
            }
            catch
            {
                shareFilePath = imageFilePath;
            }
        }

        dataTransferManager.DataRequested += async (sender, args) =>
        {
            DataRequestDeferral deferral = args.Request.GetDeferral();
            try
            {
                args.Request.Data.Properties.Title = title;
                args.Request.Data.Properties.Description = "Share image from Magick Crop & Measure";

                StorageFile file = await StorageFile.GetFileFromPathAsync(shareFilePath);
                args.Request.Data.SetStorageItems([file]);
                args.Request.Data.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
            }
            catch
            {
                args.Request.FailWithDisplayText("Failed to prepare image for sharing.");
            }
            finally
            {
                deferral.Complete();
            }
        };

        interop.ShowShareUIForWindow(hwnd);
    }

    [ComImport]
    [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        nint GetForWindow([In] nint appWindow, [In] ref Guid riid);
        void ShowShareUIForWindow(nint appWindow);
    }
}
