using System.IO;
using System.Windows;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage.Streams;
using IStorageFile = Windows.Storage.IStorageFile;
using ShareOperation = Windows.ApplicationModel.DataTransfer.ShareTarget.ShareOperation;
using StandardDataFormats = Windows.ApplicationModel.DataTransfer.StandardDataFormats;

namespace MagickCrop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0 && File.Exists(e.Args[0])
            && Path.GetExtension(e.Args[0]).Equals(".mcm", StringComparison.OrdinalIgnoreCase))
        {
            MainWindow mainWindow = new();
            mainWindow.LoadMeasurementsPackageFromFile(e.Args[0]);
            mainWindow.Show();
            return;
        }

        // Check for Share Target activation (packaged app only)
        if (TryHandleShareActivation())
            return;

        MainWindow normalMainWindow = new();
        normalMainWindow.Show();
    }

    private bool TryHandleShareActivation()
    {
        try
        {
            IActivatedEventArgs? activationArgs = AppInstance.GetActivatedEventArgs();
            if (activationArgs is ShareTargetActivatedEventArgs shareArgs)
            {
                HandleShareAsync(shareArgs);
                return true;
            }
        }
        catch
        {
            // Not running as a packaged app, or activation args unavailable
        }

        return false;
    }

    private async void HandleShareAsync(ShareTargetActivatedEventArgs args)
    {
        ShareOperation shareOperation = args.ShareOperation;
        shareOperation.ReportStarted();

        MainWindow mainWindow = new();
        mainWindow.Show();

        try
        {
            if (shareOperation.Data.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<global::Windows.Storage.IStorageItem> items = await shareOperation.Data.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is IStorageFile file)
                {
                    string path = file.Path;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        if (Path.GetExtension(path).Equals(".mcm", StringComparison.OrdinalIgnoreCase))
                            mainWindow.LoadMeasurementsPackageFromFile(path);
                        else
                            await mainWindow.OpenSharedImageAsync(path);
                    }
                }
            }
            else if (shareOperation.Data.Contains(StandardDataFormats.Bitmap))
            {
                RandomAccessStreamReference bitmapRef = await shareOperation.Data.GetBitmapAsync();
                using IRandomAccessStreamWithContentType stream = await bitmapRef.OpenReadAsync();

                string tempPath = Path.Combine(Path.GetTempPath(), $"MagickCrop_Share_{Guid.NewGuid()}.png");
                using (FileStream fileStream = new(tempPath, FileMode.Create, FileAccess.Write))
                {
                    await stream.AsStreamForRead().CopyToAsync(fileStream);
                }

                if (File.Exists(tempPath))
                    await mainWindow.OpenSharedImageAsync(tempPath);
            }
        }
        catch
        {
            // Gracefully handle share failures
        }
        finally
        {
            shareOperation.ReportCompleted();
        }
    }
}

