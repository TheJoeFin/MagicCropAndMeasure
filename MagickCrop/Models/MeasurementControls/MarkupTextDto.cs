namespace MagickCrop.Models.MeasurementControls;

public class MarkupTextDto : MeasurementControlDto
{
    public MarkupTextDto()
    {
        Type = "MarkupText";
    }

    public string Text { get; set; } = string.Empty;
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public string TextColor { get; set; } = "#FFFF0000";
    public double FontSize { get; set; } = 16.0;
}
