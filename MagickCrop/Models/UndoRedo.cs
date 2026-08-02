using ImageMagick;
using MagickCrop.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace MagickCrop;

public partial class UndoRedo : INotifyPropertyChanged
{
    private readonly Stack<UndoRedoItem> _undoStack = new();
    private readonly Stack<UndoRedoItem> _redoStack = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string CurrentPath { get; set; } = string.Empty;

    public void AddUndo(UndoRedoItem item)
    {
        _undoStack.Push(item);
        _redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public string Undo()
    {
        if (_undoStack.Count == 0)
            return string.Empty;

        UndoRedoItem item = _undoStack.Pop();
        string newPath = item.Undo();
        _redoStack.Push(item);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        return newPath;
    }

    public string Redo()
    {
        if (_redoStack.Count == 0)
            return string.Empty;

        UndoRedoItem item = _redoStack.Pop();
        string newPath = item.Redo();
        _undoStack.Push(item);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        return newPath;
    }

    internal void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public abstract class UndoRedoItem
{
    public abstract string Undo();
    public abstract string Redo();
}


public class MagickImageUndoRedoItem : UndoRedoItem
{
    private readonly Image _image;
    private readonly string _previous;
    private readonly string _next;

    public MagickImageUndoRedoItem(Image image, string previousPath, string nextPath)
    {
        _image = image;
        _previous = previousPath;
        _next = nextPath;
    }

    public override string Undo()
    {
        using MagickImage undoImage = new(_previous);
        _image.Source = undoImage.ToBitmapSource();
        return _previous;
    }

    public override string Redo()
    {
        using MagickImage redoImage = new(_next);
        _image.Source = redoImage.ToBitmapSource();
        return _next;
    }
}

public class MarkupShapeAddedItem : UndoRedoItem
{
    private readonly MarkupShapeControl _control;
    private readonly ObservableCollection<MarkupShapeControl> _collection;
    private readonly Canvas _canvas;
    private readonly Action _wireEvents;
    private readonly Action _unwireEvents;

    public MarkupShapeAddedItem(
        MarkupShapeControl control,
        ObservableCollection<MarkupShapeControl> collection,
        Canvas canvas,
        Action wireEvents,
        Action unwireEvents)
    {
        _control = control;
        _collection = collection;
        _canvas = canvas;
        _wireEvents = wireEvents;
        _unwireEvents = unwireEvents;
    }

    public override string Undo()
    {
        _unwireEvents();
        _collection.Remove(_control);
        _canvas.Children.Remove(_control);
        return string.Empty;
    }

    public override string Redo()
    {
        _wireEvents();
        _collection.Add(_control);
        _canvas.Children.Add(_control);
        return string.Empty;
    }
}

public class MarkupShapePointMovedItem : UndoRedoItem
{
    private readonly MarkupShapeControl _control;
    private readonly int _pointIndex;
    private readonly Point _before;
    private readonly Point _after;

    public MarkupShapePointMovedItem(MarkupShapeControl control, int pointIndex, Point before, Point after)
    {
        _control = control;
        _pointIndex = pointIndex;
        _before = before;
        _after = after;
    }

    public override string Undo()
    {
        _control.MovePoint(_pointIndex, _before);
        return string.Empty;
    }

    public override string Redo()
    {
        _control.MovePoint(_pointIndex, _after);
        return string.Empty;
    }
}

public class MarkupTextAddedItem : UndoRedoItem
{
    private readonly MarkupTextControl _control;
    private readonly ObservableCollection<MarkupTextControl> _collection;
    private readonly Canvas _canvas;
    private readonly Action _wireEvents;
    private readonly Action _unwireEvents;

    public MarkupTextAddedItem(
        MarkupTextControl control,
        ObservableCollection<MarkupTextControl> collection,
        Canvas canvas,
        Action wireEvents,
        Action unwireEvents)
    {
        _control = control;
        _collection = collection;
        _canvas = canvas;
        _wireEvents = wireEvents;
        _unwireEvents = unwireEvents;
    }

    public override string Undo()
    {
        _unwireEvents();
        _collection.Remove(_control);
        _canvas.Children.Remove(_control);
        return string.Empty;
    }

    public override string Redo()
    {
        _wireEvents();
        _collection.Add(_control);
        _canvas.Children.Add(_control);
        return string.Empty;
    }
}

public class MarkupStrokeAddedItem : UndoRedoItem
{
    private readonly InkCanvas _canvas;
    private readonly Stroke _stroke;

    public MarkupStrokeAddedItem(InkCanvas canvas, Stroke stroke)
    {
        _canvas = canvas;
        _stroke = stroke;
    }

    public override string Undo()
    {
        _canvas.Strokes.Remove(_stroke);
        return string.Empty;
    }

    public override string Redo()
    {
        if (!_canvas.Strokes.Contains(_stroke))
            _canvas.Strokes.Add(_stroke);
        return string.Empty;
    }
}

public class MarkupStrokeBatchAddedItem : UndoRedoItem
{
    private readonly InkCanvas _canvas;
    private readonly List<Stroke> _strokes;

    public MarkupStrokeBatchAddedItem(InkCanvas canvas, List<Stroke> strokes)
    {
        _canvas = canvas;
        _strokes = strokes;
    }

    public override string Undo()
    {
        foreach (Stroke stroke in _strokes)
            _canvas.Strokes.Remove(stroke);
        return string.Empty;
    }

    public override string Redo()
    {
        foreach (Stroke stroke in _strokes)
            if (!_canvas.Strokes.Contains(stroke))
                _canvas.Strokes.Add(stroke);
        return string.Empty;
    }
}

public class MarkupStrokeMovedItem : UndoRedoItem
{
    private readonly StrokeCollection _strokes;
    private readonly double _deltaX;
    private readonly double _deltaY;

    public MarkupStrokeMovedItem(StrokeCollection strokes, double deltaX, double deltaY)
    {
        _strokes = new StrokeCollection(strokes);
        _deltaX = deltaX;
        _deltaY = deltaY;
    }

    public override string Undo()
    {
        Matrix m = new();
        m.Translate(-_deltaX, -_deltaY);
        foreach (Stroke s in _strokes)
            s.Transform(m, false);
        return string.Empty;
    }

    public override string Redo()
    {
        Matrix m = new();
        m.Translate(_deltaX, _deltaY);
        foreach (Stroke s in _strokes)
            s.Transform(m, false);
        return string.Empty;
    }
}

public class MarkupGroupMovedItem : UndoRedoItem
{
    private readonly StrokeCollection _strokes;
    private readonly List<MarkupShapeControl> _shapes;
    private readonly List<MarkupTextControl> _texts;
    private readonly double _deltaX;
    private readonly double _deltaY;

    public MarkupGroupMovedItem(
        StrokeCollection strokes,
        List<MarkupShapeControl> shapes,
        List<MarkupTextControl> texts,
        double deltaX,
        double deltaY)
    {
        _strokes = new StrokeCollection(strokes);
        _shapes = shapes;
        _texts = texts;
        _deltaX = deltaX;
        _deltaY = deltaY;
    }

    public override string Undo()
    {
        Apply(-_deltaX, -_deltaY);
        return string.Empty;
    }

    public override string Redo()
    {
        Apply(_deltaX, _deltaY);
        return string.Empty;
    }

    private void Apply(double deltaX, double deltaY)
    {
        if (_strokes.Count > 0)
        {
            Matrix m = new();
            m.Translate(deltaX, deltaY);
            foreach (Stroke s in _strokes)
                s.Transform(m, false);
        }

        foreach (MarkupShapeControl shape in _shapes)
        {
            (Point p1, Point p2) = shape.GetPoints();
            shape.MovePoint(0, new Point(p1.X + deltaX, p1.Y + deltaY));
            shape.MovePoint(1, new Point(p2.X + deltaX, p2.Y + deltaY));
        }

        foreach (MarkupTextControl text in _texts)
        {
            Canvas.SetLeft(text, Canvas.GetLeft(text) + deltaX);
            Canvas.SetTop(text, Canvas.GetTop(text) + deltaY);
        }
    }
}

public class MarkupStrokeDeletedItem : UndoRedoItem
{
    private readonly InkCanvas _canvas;
    private readonly List<Stroke> _strokes;

    public MarkupStrokeDeletedItem(InkCanvas canvas, StrokeCollection strokes)
    {
        _canvas = canvas;
        _strokes = [.. strokes];
    }

    public override string Undo()
    {
        foreach (Stroke s in _strokes)
            if (!_canvas.Strokes.Contains(s))
                _canvas.Strokes.Add(s);
        return string.Empty;
    }

    public override string Redo()
    {
        foreach (Stroke s in _strokes)
            _canvas.Strokes.Remove(s);
        return string.Empty;
    }
}

public class MarkupStrokePropertiesChangedItem : UndoRedoItem
{
    private readonly List<(Stroke stroke, DrawingAttributes before, DrawingAttributes after)> _changes;

    public MarkupStrokePropertiesChangedItem(List<(Stroke, DrawingAttributes, DrawingAttributes)> changes)
    {
        _changes = changes;
    }

    public override string Undo()
    {
        foreach ((Stroke? stroke, DrawingAttributes? before, DrawingAttributes _) in _changes)
            stroke.DrawingAttributes = before;
        return string.Empty;
    }

    public override string Redo()
    {
        foreach ((Stroke? stroke, DrawingAttributes _, DrawingAttributes? after) in _changes)
            stroke.DrawingAttributes = after;
        return string.Empty;
    }
}

public class MarkupControlRemovedItem<T> : UndoRedoItem where T : UIElement
{
    private readonly T _control;
    private readonly ObservableCollection<T> _collection;
    private readonly Canvas _canvas;
    private readonly Action _wireEvents;
    private readonly Action _unwireEvents;

    public MarkupControlRemovedItem(
        T control,
        ObservableCollection<T> collection,
        Canvas canvas,
        Action wireEvents,
        Action unwireEvents)
    {
        _control = control;
        _collection = collection;
        _canvas = canvas;
        _wireEvents = wireEvents;
        _unwireEvents = unwireEvents;
    }

    public override string Undo()
    {
        _wireEvents();
        _collection.Add(_control);
        _canvas.Children.Add(_control);
        return string.Empty;
    }

    public override string Redo()
    {
        _unwireEvents();
        _collection.Remove(_control);
        _canvas.Children.Remove(_control);
        return string.Empty;
    }
}

public class MarkupTextMovedItem : UndoRedoItem
{
    private readonly MarkupTextControl _control;
    private readonly Point _before;
    private readonly Point _after;

    public MarkupTextMovedItem(MarkupTextControl control, Point before, Point after)
    {
        _control = control;
        _before = before;
        _after = after;
    }

    public override string Undo()
    {
        Canvas.SetLeft(_control, _before.X);
        Canvas.SetTop(_control, _before.Y);
        return string.Empty;
    }

    public override string Redo()
    {
        Canvas.SetLeft(_control, _after.X);
        Canvas.SetTop(_control, _after.Y);
        return string.Empty;
    }
}

public class MarkupTextChangedItem : UndoRedoItem
{
    private readonly MarkupTextControl _control;
    private readonly string _before;
    private readonly string _after;

    public MarkupTextChangedItem(MarkupTextControl control, string before, string after)
    {
        _control = control;
        _before = before;
        _after = after;
    }

    public override string Undo()
    {
        _control.MarkupText = _before;
        return string.Empty;
    }

    public override string Redo()
    {
        _control.MarkupText = _after;
        return string.Empty;
    }
}

public class MarkupStrokeResizedItem : UndoRedoItem
{
    private readonly StrokeCollection _strokes;
    private readonly Rect _before;
    private readonly Rect _after;

    public MarkupStrokeResizedItem(StrokeCollection strokes, Rect before, Rect after)
    {
        _strokes = new StrokeCollection(strokes);
        _before = before;
        _after = after;
    }

    public override string Undo()
    {
        Apply(_after, _before);
        return string.Empty;
    }

    public override string Redo()
    {
        Apply(_before, _after);
        return string.Empty;
    }

    private void Apply(Rect from, Rect to)
    {
        Matrix m = new();
        m.Translate(-from.X, -from.Y);
        m.Scale(to.Width / from.Width, to.Height / from.Height);
        m.Translate(to.X, to.Y);
        foreach (Stroke s in _strokes)
            s.Transform(m, false);
    }
}

public class MarkupClearedItem : UndoRedoItem
{
    private readonly List<MarkupShapeControl> _shapes;
    private readonly List<MarkupTextControl> _texts;
    private readonly List<Stroke> _strokes;
    private readonly ObservableCollection<MarkupShapeControl> _shapeCollection;
    private readonly ObservableCollection<MarkupTextControl> _textCollection;
    private readonly Canvas _canvas;
    private readonly InkCanvas _inkCanvas;
    private readonly Action _wireEvents;
    private readonly Action _unwireEvents;

    public MarkupClearedItem(
        List<MarkupShapeControl> shapes,
        List<MarkupTextControl> texts,
        List<Stroke> strokes,
        ObservableCollection<MarkupShapeControl> shapeCollection,
        ObservableCollection<MarkupTextControl> textCollection,
        Canvas canvas,
        InkCanvas inkCanvas,
        Action wireEvents,
        Action unwireEvents)
    {
        _shapes = shapes;
        _texts = texts;
        _strokes = strokes;
        _shapeCollection = shapeCollection;
        _textCollection = textCollection;
        _canvas = canvas;
        _inkCanvas = inkCanvas;
        _wireEvents = wireEvents;
        _unwireEvents = unwireEvents;
    }

    public override string Undo()
    {
        _wireEvents();
        foreach (MarkupShapeControl shape in _shapes)
        {
            _shapeCollection.Add(shape);
            _canvas.Children.Add(shape);
        }
        foreach (MarkupTextControl text in _texts)
        {
            _textCollection.Add(text);
            _canvas.Children.Add(text);
        }
        foreach (Stroke stroke in _strokes)
            if (!_inkCanvas.Strokes.Contains(stroke))
                _inkCanvas.Strokes.Add(stroke);
        return string.Empty;
    }

    public override string Redo()
    {
        _unwireEvents();
        foreach (MarkupShapeControl shape in _shapes)
        {
            _shapeCollection.Remove(shape);
            _canvas.Children.Remove(shape);
        }
        foreach (MarkupTextControl text in _texts)
        {
            _textCollection.Remove(text);
            _canvas.Children.Remove(text);
        }
        foreach (Stroke stroke in _strokes)
            _inkCanvas.Strokes.Remove(stroke);
        return string.Empty;
    }
}

public class ResizeUndoRedoItem : UndoRedoItem
{
    private readonly Image _image;
    private readonly string _previous;
    private readonly string _next;
    private readonly Grid _grid;
    private readonly Size _oldSize;

    public ResizeUndoRedoItem(Image image, Grid grid, Size oldGridSize, string previousPath, string nextPath)
    {
        _image = image;
        _previous = previousPath;
        _next = nextPath;
        _grid = grid;
        _oldSize = oldGridSize;
    }

    public override string Undo()
    {
        using MagickImage undoImage = new(_previous);
        _image.Source = undoImage.ToBitmapSource();
        _image.Stretch = Stretch.Uniform;
        _grid.Width = 700;
        _grid.Height = double.NaN;
        return _previous;
    }

    public override string Redo()
    {
        using MagickImage redoImage = new(_next);
        _image.Source = redoImage.ToBitmapSource();
        _image.Stretch = Stretch.Fill;
        _grid.Width = _oldSize.Width;
        _grid.Height = _oldSize.Height;
        return _next;
    }
}