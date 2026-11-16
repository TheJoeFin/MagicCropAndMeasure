using MagickCrop.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace MagickCrop.Controls;

public enum AspectRatioDefaultChoice
{
    KeepCurrent,
    UseSelected,
    AskEveryTime,
}

public partial class AspectRatioPicker : UserControl
{
    private readonly List<AspectRatioItem> _standardRatios;
    private readonly ObservableCollection<AspectRatioItem> _savedRatios;
    private readonly string? _defaultRatioId;
    private readonly double? _originalRatio;
    private bool _updatingSelection;

    public AspectRatioPicker(
        IEnumerable<AspectRatioItem> standardRatios,
        IEnumerable<AspectRatioItem> savedRatios,
        AspectRatioItem? selectedRatio,
        string? defaultRatioId,
        double? originalRatio)
    {
        InitializeComponent();

        _standardRatios = standardRatios.Select(item => item with { }).ToList();
        _savedRatios = new ObservableCollection<AspectRatioItem>(
            savedRatios.Select(item => item with { }));
        _defaultRatioId = defaultRatioId;
        _originalRatio = originalRatio;

        StandardRatioList.ItemsSource = _standardRatios;
        SavedRatioList.ItemsSource = _savedRatios;

        AspectRatioItem? currentDefault = _standardRatios
            .Concat(_savedRatios)
            .FirstOrDefault(item => item.Id == _defaultRatioId);

        if (currentDefault is null)
        {
            KeepDefaultRadio.Visibility = Visibility.Collapsed;
            AskEveryTimeRadio.IsChecked = true;
        }
        else
        {
            KeepDefaultRadio.Content = $"Keep my current default ({currentDefault.Text})";
            KeepDefaultRadio.IsChecked = true;
        }

        if (selectedRatio is not null)
        {
            AspectRatioItem? matchingItem = _standardRatios
                .Concat(_savedRatios)
                .FirstOrDefault(item => item.Id == selectedRatio.Id);
            SelectRatio(matchingItem ?? selectedRatio with { });
        }
        else
        {
            SelectRatio(null);
        }
    }

    public event EventHandler? SelectedRatioChanged;

    public AspectRatioItem? SelectedRatio { get; private set; }

    public IReadOnlyList<AspectRatioItem> SavedRatios =>
        _savedRatios.Select(item => item with { }).ToList();

    public AspectRatioDefaultChoice DefaultChoice
    {
        get
        {
            if (UseSelectedAsDefaultRadio.IsChecked == true)
                return AspectRatioDefaultChoice.UseSelected;

            if (AskEveryTimeRadio.IsChecked == true)
                return AspectRatioDefaultChoice.AskEveryTime;

            return AspectRatioDefaultChoice.KeepCurrent;
        }
    }

    private void RatioList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection || sender is not ListBox listBox || listBox.SelectedItem is not AspectRatioItem item)
            return;

        SelectRatio(item);
    }

    private void SelectRatio(AspectRatioItem? item)
    {
        _updatingSelection = true;

        SelectedRatio = item;
        StandardRatioList.SelectedItem = item;
        SavedRatioList.SelectedItem = item;

        if (item is null)
        {
            SelectedRatioTitle.Text = "No ratio selected";
            SelectedRatioDescription.Text = "Pick a standard or saved ratio, or create a custom one below.";
            SelectionPreview.RatioItem = null;
            DeleteSavedRatioButton.IsEnabled = false;
            UseSelectedAsDefaultRadio.IsEnabled = false;
        }
        else
        {
            SelectedRatioTitle.Text = item.Text;
            SelectedRatioDescription.Text = item.ToolTip;

            AspectRatioItem previewItem = item with { };
            if (item.AspectRatioEnum == AspectRatio.Original && _originalRatio is > 0)
                previewItem.RatioValue = _originalRatio.Value;

            SelectionPreview.RatioItem = previewItem;
            DeleteSavedRatioButton.IsEnabled = _savedRatios.Any(saved => saved.Id == item.Id);
            UseSelectedAsDefaultRadio.IsEnabled = IsPersisted(item);
        }

        if (IsLoaded
            && UseSelectedAsDefaultRadio.IsChecked == true
            && !IsPersisted(item))
        {
            if (KeepDefaultRadio.Visibility == Visibility.Visible)
                KeepDefaultRadio.IsChecked = true;
            else
                AskEveryTimeRadio.IsChecked = true;
        }

        _updatingSelection = false;

        if (IsLoaded)
            SelectedRatioChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool IsPersisted(AspectRatioItem? item) =>
        item is not null
        && (_standardRatios.Any(ratio => ratio.Id == item.Id)
            || _savedRatios.Any(ratio => ratio.Id == item.Id));

    private void UseCustomRatioButton_Click(object sender, RoutedEventArgs e)
    {
        double? width = CustomWidthInput.Value;
        double? height = CustomHeightInput.Value;

        if (width is null or <= 0 || height is null or <= 0
            || !double.IsFinite(width.Value) || !double.IsFinite(height.Value))
        {
            ShowValidationMessage("Enter a width and height greater than zero.");
            return;
        }

        bool saveRatio = SaveCustomRatioCheckBox.IsChecked == true;
        string name = CustomNameInput.Text?.Trim() ?? string.Empty;

        if (saveRatio && string.IsNullOrWhiteSpace(name))
        {
            ShowValidationMessage("Enter a name before saving this ratio.");
            return;
        }

        if (saveRatio && _savedRatios.Any(item =>
            string.Equals(item.Text, name, StringComparison.OrdinalIgnoreCase)))
        {
            ShowValidationMessage("A saved ratio with that name already exists.");
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
            name = $"Custom {width.Value:0.##}:{height.Value:0.##}";

        AspectRatioItem customRatio = AspectRatioItem.CreateCustom(
            name,
            width.Value,
            height.Value,
            CustomUnitsInput.Text);

        if (saveRatio)
            _savedRatios.Add(customRatio);

        ValidationMessage.Visibility = Visibility.Collapsed;
        SelectRatio(customRatio);
    }

    private void DeleteSavedRatioButton_Click(object sender, RoutedEventArgs e)
    {
        if (SavedRatioList.SelectedItem is not AspectRatioItem item)
            return;

        _savedRatios.Remove(item);
        SelectRatio(null);
    }

    private void ShowValidationMessage(string message)
    {
        ValidationMessage.Text = message;
        ValidationMessage.Visibility = Visibility.Visible;
    }
}
