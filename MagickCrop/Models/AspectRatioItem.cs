namespace MagickCrop.Models;

public record AspectRatioItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ToolTip { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double RatioValue { get; set; } = 0;
    public AspectRatio AspectRatioEnum { get; set; }
    public double? RealWorldWidth { get; set; }
    public double? RealWorldHeight { get; set; }
    public string? RealWorldUnits { get; set; }

    public static AspectRatioItem CreateCustom(
        string name,
        double width,
        double height,
        string? realWorldUnits = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!double.IsFinite(width) || width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        if (!double.IsFinite(height) || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        string units = realWorldUnits?.Trim() ?? string.Empty;

        return new AspectRatioItem
        {
            Id = $"custom-{Guid.NewGuid():N}",
            Name = name.Trim(),
            Text = name.Trim(),
            Symbol = "TableEdit24",
            ToolTip = string.IsNullOrEmpty(units)
                ? $"{width:0.##} wide by {height:0.##} high"
                : $"{width:0.##} wide by {height:0.##} high {units}",
            RatioValue = height / width,
            AspectRatioEnum = AspectRatio.Custom,
            RealWorldWidth = string.IsNullOrEmpty(units) ? null : width,
            RealWorldHeight = string.IsNullOrEmpty(units) ? null : height,
            RealWorldUnits = string.IsNullOrEmpty(units) ? null : units,
        };
    }

    public static bool IsValidCustom(AspectRatioItem item) =>
        item.AspectRatioEnum == AspectRatio.Custom
        && !string.IsNullOrWhiteSpace(item.Id)
        && !string.IsNullOrWhiteSpace(item.Text)
        && double.IsFinite(item.RatioValue)
        && item.RatioValue > 0;

    public static List<AspectRatioItem> GetStandardAspectRatios() =>
        [
            new AspectRatioItem
            {
                Id = "original",
                ToolTip = "Original image aspect ratio",
                Symbol = "Image24",
                Text = "Original",
                RatioValue = 1,
                AspectRatioEnum = AspectRatio.Original
            },
            new AspectRatioItem
            {
                Id = "square",
                ToolTip = "1 wide by 1 high",
                Symbol = "RatioOneToOne24",
                Text = "Square 1:1",
                RatioValue = 1,
                AspectRatioEnum = AspectRatio.Square
            },
            new AspectRatioItem
            {
                Id = "photo-landscape-3x2",
                ToolTip = "3 wide by 2 high",
                Symbol = "Image24",
                Text = "Photo Landscape 3:2",
                RatioValue = 2.0 / 3.0,
                AspectRatioEnum = AspectRatio.PhotoLandscape3By2
            },
            new AspectRatioItem
            {
                Id = "photo-portrait-2x3",
                ToolTip = "2 wide by 3 high",
                Symbol = "Image24",
                Text = "Photo Portrait 2:3",
                RatioValue = 3.0 / 2.0,
                AspectRatioEnum = AspectRatio.PhotoPortrait2By3
            },
            new AspectRatioItem
            {
                Id = "classic-landscape-4x3",
                ToolTip = "4 wide by 3 high",
                Symbol = "Image24",
                Text = "Classic Landscape 4:3",
                RatioValue = 3.0 / 4.0,
                AspectRatioEnum = AspectRatio.ClassicLandscape4By3
            },
            new AspectRatioItem
            {
                Id = "classic-portrait-3x4",
                ToolTip = "3 wide by 4 high",
                Symbol = "Image24",
                Text = "Classic Portrait 3:4",
                RatioValue = 4.0 / 3.0,
                AspectRatioEnum = AspectRatio.ClassicPortrait3By4
            },
            new AspectRatioItem
            {
                Id = "widescreen-landscape-16x9",
                ToolTip = "16 wide by 9 high",
                Symbol = "Image24",
                Text = "Widescreen 16:9",
                RatioValue = 9.0 / 16.0,
                AspectRatioEnum = AspectRatio.WidescreenLandscape16By9
            },
            new AspectRatioItem
            {
                Id = "widescreen-portrait-9x16",
                ToolTip = "9 wide by 16 high",
                Symbol = "Image24",
                Text = "Portrait Video 9:16",
                RatioValue = 16.0 / 9.0,
                AspectRatioEnum = AspectRatio.WidescreenPortrait9By16
            },
            new AspectRatioItem
            {
                Id = "letter-landscape",
                ToolTip = "8.5 tall by 11 wide",
                Symbol = "DocumentLandscape24",
                Text = "US Letter Landscape 11:8.5",
                RatioValue = 8.5 / 11,
                AspectRatioEnum = AspectRatio.LetterLandscape,
                RealWorldWidth = 11,
                RealWorldHeight = 8.5,
                RealWorldUnits = "in",
            },
            new AspectRatioItem
            {
                Id = "letter-portrait",
                ToolTip = "11 tall by 8.5 wide",
                Symbol = "Document24",
                Text = "US Letter Portrait 8.5:11",
                RatioValue = 11 / 8.5,
                AspectRatioEnum = AspectRatio.LetterPortrait,
                RealWorldWidth = 8.5,
                RealWorldHeight = 11,
                RealWorldUnits = "in",
            },
            new AspectRatioItem
            {
                Id = "a4-landscape",
                ToolTip = "210 tall by 297 wide",
                Symbol = "DocumentLandscape24",
                Text = "A4 Landscape 297:210",
                RatioValue = 210.0 / 297.0,
                AspectRatioEnum = AspectRatio.A4Landscape,
                RealWorldWidth = 297,
                RealWorldHeight = 210,
                RealWorldUnits = "mm",
            },
            new AspectRatioItem
            {
                Id = "a4-portrait",
                ToolTip = "297 tall by 210 wide",
                Symbol = "Document24",
                Text = "A4 Portrait 210:297",
                RatioValue = 297.0 / 210.0,
                AspectRatioEnum = AspectRatio.A4Portrait,
                RealWorldWidth = 210,
                RealWorldHeight = 297,
                RealWorldUnits = "mm",
            },
            new AspectRatioItem
            {
                Id = "us-bill-portrait",
                ToolTip = "6.14 tall by 2.61 wide",
                Symbol = "AlignEndHorizontal20",
                Text = "US Bill Portrait",
                RatioValue = 6.14 / 2.61,
                AspectRatioEnum = AspectRatio.UsDollarBillPortrait,
                RealWorldWidth = 2.61,
                RealWorldHeight = 6.14,
                RealWorldUnits = "in",
            },
            new AspectRatioItem
            {
                Id = "us-bill-landscape",
                ToolTip = "2.61 tall by 6.14 wide",
                Symbol = "AlignEndVertical20",
                Text = "US Bill Landscape",
                RatioValue = 2.61 / 6.14,
                AspectRatioEnum = AspectRatio.UsDollarBillLandscape,
                RealWorldWidth = 6.14,
                RealWorldHeight = 2.61,
                RealWorldUnits = "in",
            }
        ];
}
