using MagickCrop.Models;
using MagickCrop.Controls;
using System.Windows;

namespace MagickCrop.Tests;

[TestClass]
public class AspectRatioItemTests
{
    [STATestMethod]
    [DoNotParallelize]
    public void AspectRatioPickerLoadsItsVisualTree()
    {
        if (Application.Current is null)
        {
            App app = new();
            app.InitializeComponent();
        }

        AspectRatioPicker picker = new(
            AspectRatioItem.GetStandardAspectRatios(),
            [],
            selectedRatio: null,
            defaultRatioId: null,
            originalRatio: null);
        Window window = new()
        {
            Content = picker,
            Width = 800,
            Height = 650,
            ShowInTaskbar = false,
        };

        window.Show();
        window.UpdateLayout();
        window.Close();
    }

    [TestMethod]
    public void StandardRatiosHaveUniqueIdsAndUsableValues()
    {
        List<AspectRatioItem> ratios = AspectRatioItem.GetStandardAspectRatios();

        Assert.HasCount(ratios.Count, ratios.Select(ratio => ratio.Id).Distinct());
        Assert.IsTrue(ratios.All(ratio => !string.IsNullOrWhiteSpace(ratio.Id)));
        Assert.IsTrue(ratios.All(ratio => double.IsFinite(ratio.RatioValue) && ratio.RatioValue > 0));
        Assert.IsFalse(ratios.Any(ratio => ratio.AspectRatioEnum == AspectRatio.Custom));
    }

    [TestMethod]
    public void CustomRatioUsesHeightOverWidthConvention()
    {
        AspectRatioItem ratio = AspectRatioItem.CreateCustom("Test card", 4, 3);

        Assert.AreEqual(0.75, ratio.RatioValue, 0.000001);
        Assert.AreEqual(AspectRatio.Custom, ratio.AspectRatioEnum);
        Assert.AreEqual("Test card", ratio.Text);
        Assert.IsTrue(AspectRatioItem.IsValidCustom(ratio));
        Assert.IsNull(ratio.RealWorldWidth);
        Assert.IsNull(ratio.RealWorldHeight);
        Assert.IsNull(ratio.RealWorldUnits);
    }

    [TestMethod]
    public void CustomRatioWithUnitsPreservesMeasurementScale()
    {
        AspectRatioItem ratio = AspectRatioItem.CreateCustom("Specimen", 25, 10, " mm ");

        Assert.AreEqual(25, ratio.RealWorldWidth);
        Assert.AreEqual(10, ratio.RealWorldHeight);
        Assert.AreEqual("mm", ratio.RealWorldUnits);
    }

    [TestMethod]
    public void CustomRatioRejectsNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AspectRatioItem.CreateCustom("Invalid", 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AspectRatioItem.CreateCustom("Invalid", 10, -1));
    }
}
