using MagickCrop.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace MagickCrop.Helpers;

/// <summary>
/// Shows a <see cref="ColorSwatchPicker"/> in a modal dialog, using the same
/// DialogHost = Presenter pattern already established for this app's other dialogs
/// (e.g. the "Change Thickness" dialog on the guide-line controls).
/// </summary>
public static class ColorPickerDialog
{
    public static async Task<Color?> PickColorAsync(MainWindow owner, Color currentColor, string title = "Change Color")
    {
        ColorSwatchPicker picker = new() { SelectedColor = currentColor };

        ContentDialog dialog = new()
        {
            Title = title,
            Content = picker,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DialogHost = owner.Presenter
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? picker.SelectedColor : null;
    }
}
