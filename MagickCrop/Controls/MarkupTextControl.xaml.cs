using MagickCrop.Helpers;
using MagickCrop.Models.MeasurementControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MagickCrop.Controls;

public partial class MarkupTextControl : UserControl
{
    private Point dragOffset;
    private Point positionBeforeDrag;
    private bool isDragging;
    private string textBeforeEdit = "Text";

    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    public delegate void TextMovedEventHandler(object sender, Point before, Point after);
    public event TextMovedEventHandler? TextMoved;

    public event EventHandler? EditCommitted;
    public event EventHandler? EditCancelled;

    public bool IsEditing => EditBox.Visibility == Visibility.Visible;

    /// <summary>
    /// The text as it was when the current or most recent edit began.
    /// </summary>
    public string TextBeforeEdit => textBeforeEdit;

    private Color textColor = Colors.Red;
    public Color TextColor
    {
        get => textColor;
        set
        {
            textColor = value;
            SolidColorBrush brush = new(textColor);
            DisplayText.Foreground = brush;
            EditBox.Foreground = brush;
            EditBox.CaretBrush = brush;
        }
    }

    private double markupFontSize = 16.0;
    public double MarkupFontSize
    {
        get => markupFontSize;
        set
        {
            markupFontSize = value;
            DisplayText.FontSize = markupFontSize;
            EditBox.FontSize = markupFontSize;
        }
    }

    public string MarkupText
    {
        get => DisplayText.Text;
        set
        {
            DisplayText.Text = value;
            EditBox.Text = value;
        }
    }

    public MarkupTextControl()
    {
        InitializeComponent();
    }

    public void EnterEditMode()
    {
        textBeforeEdit = DisplayText.Text;
        EditBox.Text = DisplayText.Text;
        DisplayText.Visibility = Visibility.Collapsed;
        EditBox.Visibility = Visibility.Visible;

        // The control may have just been added to the canvas and not be loaded
        // yet, in which case Focus() fails — defer until layout has run
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            EditBox.Focus();
            EditBox.SelectAll();
        });
    }

    public void CommitEdit() => FinishEdit(accepted: true);

    public void CancelEdit() => FinishEdit(accepted: false);

    private void FinishEdit(bool accepted)
    {
        if (!IsEditing)
            return;

        string text = EditBox.Text.Trim();
        if (text.Length == 0)
            accepted = false; // committing empty text is a cancel

        // Collapse first: the focus shift it triggers re-enters via LostFocus,
        // which the IsEditing guard above turns into a no-op
        EditBox.Visibility = Visibility.Collapsed;
        DisplayText.Visibility = Visibility.Visible;

        if (accepted)
        {
            DisplayText.Text = text;
            EditBox.Text = text;
            EditCommitted?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            DisplayText.Text = textBeforeEdit;
            EditBox.Text = textBeforeEdit;
            EditCancelled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitEdit();
    }

    private void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelEdit();
            e.Handled = true;
        }
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsEditing || e.ChangedButton != MouseButton.Left)
            return;

        if (e.ClickCount == 2)
        {
            EnterEditMode();
            e.Handled = true;
            return;
        }

        isDragging = true;
        dragOffset = e.GetPosition(this);
        positionBeforeDrag = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
        // Capture on the Border (the sender) so its MouseMove/MouseUp handlers
        // keep receiving events; capturing the UserControl routes events away
        // from the Border and the drag never updates or releases
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void Border_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isDragging || Parent is not Canvas canvas)
            return;

        Point parentPos = e.GetPosition(canvas);
        Canvas.SetLeft(this, parentPos.X - dragOffset.X);
        Canvas.SetTop(this, parentPos.Y - dragOffset.Y);
    }

    private void Border_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!isDragging)
            return;

        isDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
        e.Handled = true;

        Point positionAfterDrag = new(Canvas.GetLeft(this), Canvas.GetTop(this));
        if (Math.Abs(positionAfterDrag.X - positionBeforeDrag.X) > 0.01
            || Math.Abs(positionAfterDrag.Y - positionBeforeDrag.Y) > 0.01)
        {
            TextMoved?.Invoke(this, positionBeforeDrag, positionAfterDrag);
        }
    }

    private void Border_LostMouseCapture(object sender, MouseEventArgs e)
    {
        isDragging = false;
    }

    private void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RemoveControlRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void ChangeColorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        Color? picked = await ColorPickerDialog.PickColorAsync(mainWindow, textColor, "Change Text Color");
        if (picked is Color color)
            TextColor = color;
    }

    public MarkupTextDto ToDto()
    {
        return new MarkupTextDto
        {
            Text = DisplayText.Text,
            PositionX = Canvas.GetLeft(this),
            PositionY = Canvas.GetTop(this),
            TextColor = textColor.ToString(),
            FontSize = markupFontSize
        };
    }

    public void FromDto(MarkupTextDto dto)
    {
        MarkupText = dto.Text;
        MarkupFontSize = dto.FontSize;

        try { TextColor = (Color)ColorConverter.ConvertFromString(dto.TextColor); }
        catch { TextColor = Colors.Red; }
    }
}
