namespace MagickCrop.Helpers;

public static class MeasurementFormattingHelper
{
    public static string FormatPerimeter(double perimeter, string units)
        => $"P: {perimeter:N2} {units}";

    public static string FormatPerimeterArea(double perimeter, double area, string linearUnits)
    {
        // Derive area units as squared linear units, e.g., "cm²", "px²"
        string squared = linearUnits + "\u00B2"; // superscript 2
        return $"P: {perimeter:N2} {linearUnits}, A: {area:N2} {squared}";
    }

    public static string FormatNeedMorePoints(double perimeter, string units, int remaining)
        => $"P: {perimeter:N2} {units} (Need {remaining} more points)";

    public static string FormatClickToClose(double perimeter, string units)
        => $"P: {perimeter:N2} {units} (Click orange point to close)";
}
