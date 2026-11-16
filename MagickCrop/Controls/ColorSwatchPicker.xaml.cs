using MagickCrop.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MagickCrop.Controls;

/// <summary>
/// A swatch grid plus a custom hex entry, used wherever the user picks a color for a
/// shape. Kept deliberately simple (no color wheel) since WPF-UI's ColorPicker control
/// has no usable public API in the package version this app references.
/// </summary>
public partial class ColorSwatchPicker : UserControl
{
    private readonly List<ToggleButton> swatchButtons = [];
    private bool isUpdatingFromCode;

    private Color selectedColor = Colors.Red;
    public Color SelectedColor
    {
        get => selectedColor;
        set
        {
            selectedColor = value;
            UpdateFromColor();
        }
    }

    public ColorSwatchPicker()
    {
        InitializeComponent();
        BuildSwatches();
        UpdateFromColor();
    }

    private void BuildSwatches()
    {
        foreach ((string name, Color color) in ColorPalette.Swatches)
        {
            ToggleButton button = new()
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(2),
                Background = new SolidColorBrush(color),
                Tag = color,
                ToolTip = name
            };
            button.Checked += SwatchButton_Checked;

            swatchButtons.Add(button);
            SwatchGrid.Children.Add(button);
        }
    }

    private void SwatchButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: Color color })
            return;

        SelectedColor = color;
    }

    private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isUpdatingFromCode) return;

        try
        {
            string text = HexTextBox.Text.Trim();
            if (text.Length == 0) return;
            if (!text.StartsWith('#')) text = "#" + text;

            Color color = (Color)ColorConverter.ConvertFromString(text);
            selectedColor = color;
            PreviewSwatch.Background = new SolidColorBrush(color);
            UncheckAllSwatches();
        }
        catch
        {
            // Left as typed — an incomplete hex value while the user is still typing.
        }
    }

    private void UpdateFromColor()
    {
        isUpdatingFromCode = true;

        PreviewSwatch.Background = new SolidColorBrush(selectedColor);
        HexTextBox.Text = selectedColor.ToString();

        foreach (ToggleButton button in swatchButtons)
            button.IsChecked = button.Tag is Color color && color == selectedColor;

        isUpdatingFromCode = false;
    }

    private void UncheckAllSwatches()
    {
        foreach (ToggleButton button in swatchButtons)
            button.IsChecked = false;
    }
}
