using MagickCrop.Helpers;

namespace MagickCrop.Tests.Helpers;

[TestClass]
public class MeasurementFormattingHelperTests
{
    [TestMethod]
    public void FormatPerimeter_IncludesUnitsAndTwoDecimalPlaces()
    {
        string result = MeasurementFormattingHelper.FormatPerimeter(12.3456, "cm");

        Assert.AreEqual("P: 12.35 cm", result);
    }

    [TestMethod]
    public void FormatPerimeterArea_SquaresTheLinearUnits()
    {
        string result = MeasurementFormattingHelper.FormatPerimeterArea(10, 6.25, "cm");

        Assert.AreEqual("P: 10.00 cm, A: 6.25 cm²", result);
    }

    [TestMethod]
    public void FormatNeedMorePoints_IncludesRemainingCount()
    {
        string result = MeasurementFormattingHelper.FormatNeedMorePoints(4.5, "px", 2);

        Assert.AreEqual("P: 4.50 px (Need 2 more points)", result);
    }

    [TestMethod]
    public void FormatClickToClose_IncludesInstructionalText()
    {
        string result = MeasurementFormattingHelper.FormatClickToClose(4.5, "px");

        Assert.AreEqual("P: 4.50 px (Click orange point to close)", result);
    }
}
