using MagickCrop;

namespace MagickCrop.Tests;

[TestClass]
public class UndoRedoTests
{
    private sealed class FakeUndoRedoItem : UndoRedoItem
    {
        public int UndoCalls { get; private set; }
        public int RedoCalls { get; private set; }
        public string UndoResult { get; set; } = string.Empty;
        public string RedoResult { get; set; } = string.Empty;

        public override string Undo()
        {
            UndoCalls++;
            return UndoResult;
        }

        public override string Redo()
        {
            RedoCalls++;
            return RedoResult;
        }
    }

    [TestMethod]
    public void NewUndoRedo_HasNothingToUndoOrRedo()
    {
        UndoRedo undoRedo = new();

        Assert.IsFalse(undoRedo.CanUndo);
        Assert.IsFalse(undoRedo.CanRedo);
    }

    [TestMethod]
    public void AddUndo_MakesCanUndoTrueAndCanRedoFalse()
    {
        UndoRedo undoRedo = new();

        undoRedo.AddUndo(new FakeUndoRedoItem());

        Assert.IsTrue(undoRedo.CanUndo);
        Assert.IsFalse(undoRedo.CanRedo);
    }

    [TestMethod]
    public void Undo_InvokesItemUndoAndMovesItToRedoStack()
    {
        UndoRedo undoRedo = new();
        FakeUndoRedoItem item = new() { UndoResult = "previous.png" };
        undoRedo.AddUndo(item);

        string result = undoRedo.Undo();

        Assert.AreEqual("previous.png", result);
        Assert.AreEqual(1, item.UndoCalls);
        Assert.IsFalse(undoRedo.CanUndo);
        Assert.IsTrue(undoRedo.CanRedo);
    }

    [TestMethod]
    public void Redo_AfterUndo_InvokesItemRedoAndMovesItBackToUndoStack()
    {
        UndoRedo undoRedo = new();
        FakeUndoRedoItem item = new() { RedoResult = "next.png" };
        undoRedo.AddUndo(item);
        undoRedo.Undo();

        string result = undoRedo.Redo();

        Assert.AreEqual("next.png", result);
        Assert.AreEqual(1, item.RedoCalls);
        Assert.IsTrue(undoRedo.CanUndo);
        Assert.IsFalse(undoRedo.CanRedo);
    }

    [TestMethod]
    public void Undo_WithEmptyStack_ReturnsEmptyStringAndDoesNotThrow()
    {
        UndoRedo undoRedo = new();

        string result = undoRedo.Undo();

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void Redo_WithEmptyStack_ReturnsEmptyStringAndDoesNotThrow()
    {
        UndoRedo undoRedo = new();

        string result = undoRedo.Redo();

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void AddUndo_AfterAnUndo_ClearsTheRedoStack()
    {
        UndoRedo undoRedo = new();
        undoRedo.AddUndo(new FakeUndoRedoItem());
        undoRedo.Undo();
        Assert.IsTrue(undoRedo.CanRedo);

        undoRedo.AddUndo(new FakeUndoRedoItem());

        Assert.IsFalse(undoRedo.CanRedo);
    }
}
