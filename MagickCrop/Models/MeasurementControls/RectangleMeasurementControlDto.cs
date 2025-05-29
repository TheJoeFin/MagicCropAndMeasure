using System.Windows;

namespace MagickCrop.Models.MeasurementControls;

public class RectangleMeasurementControlDto : MeasurementControlDto
{
    public RectangleMeasurementControlDto()
    {
        Type = "Rectangle";
    }
    public Point TopLeft { get; set; }
    public Point BottomRight { get; set; }
}
