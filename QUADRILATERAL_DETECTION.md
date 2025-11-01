# Quadrilateral Detection Feature

## Overview
This feature adds automatic quadrilateral detection using OpenCV to the Transform tool in MagickCrop. When activated, it analyzes the image to find rectangular shapes (like documents, papers, signs, etc.) and allows users to quickly select one for perspective correction, instead of manually positioning all four corner markers.

## How It Works

### Detection Algorithm
The quadrilateral detection uses OpenCV's computer vision algorithms:

1. **Preprocessing**
   - Converts image to grayscale
   - Applies Gaussian blur to reduce noise
   - Uses Canny edge detection to find edges

2. **Contour Detection**
   - Finds all contours in the edge-detected image
   - Dilates edges to connect broken lines
   - Approximates contours to polygons

3. **Filtering & Ranking**
   - Filters for 4-sided convex polygons (quadrilaterals)
   - Filters out shapes smaller than 2% of image area
   - Ranks candidates by:
     - Size (60% weight) - larger shapes are typically more relevant
     - Rectangularity (40% weight) - how close angles are to 90 degrees

4. **Results**
   - Returns top 5 detected quadrilaterals
   - Displays them in a selection dialog with previews
   - Scales coordinates to match the display size

### User Interface

#### New "Detect Shape" Button
- Located in the Transform tool panel
- Appears when Transform mode is activated
- Shows a progress indicator while processing

#### Selection Dialog
When quadrilaterals are detected, a dialog overlay appears showing:
- Preview thumbnail of each detected shape
- Confidence percentage
- Ability to select a shape with a click
- "Set Manually" option to fall back to manual positioning
- "Cancel" button to dismiss without changes

#### Automatic Corner Positioning
When a shape is selected:
- All four corner markers are automatically positioned
- The polyline connecting them is updated
- User can still manually adjust corners if needed
- Transform can be applied immediately or after adjustments

## Usage

### Basic Workflow
1. Open an image containing a rectangular object
2. Click "Edit" → "Perspective Correction" (or use the Transform tool)
3. Click the "Detect Shape" button
4. If shapes are found, a selection dialog appears
5. Click on the desired quadrilateral
6. Corners are automatically positioned
7. Adjust manually if needed
8. Apply the transform

### When to Use Manual Positioning
The auto-detection works best for:
- ✅ Clear, well-lit photographs
- ✅ Documents on contrasting backgrounds
- ✅ Rectangular objects with defined edges
- ✅ Images with minimal clutter

Use manual positioning for:
- ❌ Very low contrast images
- ❌ Heavily cluttered backgrounds
- ❌ Partial or obscured rectangles
- ❌ Non-rectangular distortions

## Technical Details

### Dependencies
- **Emgu.CV** v4.10.0.5671 - .NET wrapper for OpenCV
- **Emgu.CV.runtime.windows** v4.10.0.5671 - Native OpenCV binaries for Windows

### Key Classes

#### `QuadrilateralDetector`
Helper class in `MagickCrop.Helpers` namespace.

**Main Method:**
```csharp
public static List<DetectedQuadrilateral> DetectQuadrilaterals(
    string imagePath, 
    out double imageWidth, 
    out double imageHeight, 
    double minArea = 0.05, 
    int maxResults = 5)
```

**Configuration Constants:**
- `DefaultMinArea = 0.05` - Minimum area (5% of image)
- `DefaultMaxResults = 5` - Maximum candidates to return
- `SizeWeight = 0.6` - Weight for size in confidence score
- `RectangularityWeight = 0.4` - Weight for rectangularity in confidence score

#### `QuadrilateralSelector`
UserControl in `MagickCrop.Controls` namespace for displaying and selecting detected shapes.

**Events:**
- `QuadrilateralSelected` - Fired when user selects a shape
- `ManualSelection` - Fired when user chooses manual positioning
- `Cancelled` - Fired when user cancels the dialog

### Performance Considerations
- Detection runs asynchronously to keep UI responsive
- Progress indicator shown during processing
- Image loaded once and dimensions reused
- Typical processing time: < 1 second for most images

### Error Handling
- Gracefully handles missing or invalid images
- Shows user-friendly error messages
- Falls back to manual positioning on detection failure
- Specific exception handling for file and OpenCV errors

## Configuration

### Adjustable Parameters in Code
In `MainWindow.xaml.cs`:
```csharp
private const double QuadDetectionMinArea = 0.02;  // 2% of image area
private const int QuadDetectionMaxResults = 5;     // Top 5 candidates
```

### Canny Edge Detection Thresholds
In `QuadrilateralDetector.cs`, line 138:
```csharp
CvInvoke.Canny(blurred, edges, 50, 150);  // Lower/upper thresholds
```

### Approximation Accuracy
In `QuadrilateralDetector.cs`, line 163:
```csharp
CvInvoke.ApproxPolyDP(contour, approx, 0.02 * perimeter, true);  // 2% accuracy
```

## Testing

### Test Image
The repository includes `MagickCrop/Images/LetterPaperTest.jpg` (3264x1836 pixels) for testing the detection algorithm.

### Manual Testing Procedure
1. Build the application in Visual Studio
2. Run MagickCrop
3. Open the test image or any image with rectangular objects
4. Activate Transform mode
5. Click "Detect Shape"
6. Verify shapes are detected and can be selected
7. Verify corners are positioned correctly
8. Apply transform and verify result

### Expected Behavior
- Detection completes in < 1 second
- Top-ranked result matches the most prominent rectangle
- All four corners are accurately positioned
- Manual adjustment works after auto-positioning
- Transform produces correctly de-skewed image

## Security

### Vulnerability Scan
✅ No vulnerabilities found in dependencies (GitHub Advisory Database check)
✅ CodeQL security scan passed with 0 alerts

### Security Considerations
- File path validation prevents directory traversal
- Exception handling prevents information disclosure
- No unsafe code or external process execution
- Input validation on all user-provided data

## Future Enhancements

### Potential Improvements
1. **Adjustable Sensitivity** - UI controls for detection thresholds
2. **Preview Overlays** - Show all detected shapes on the image
3. **Smart Default Selection** - Auto-select most likely candidate
4. **Undo/Redo** - Revert to previous corner positions
5. **Saved Presets** - Remember detection settings per project
6. **Batch Processing** - Detect and correct multiple images
7. **ML-based Detection** - Use trained models for better accuracy

### User-Requested Features
- Ability to see all detected shapes highlighted on the image before selection
- Option to detect specific shapes (e.g., "only documents", "only signs")
- Integration with other measurement tools

## Troubleshooting

### No Shapes Detected
**Possible causes:**
- Image has low contrast
- Background is too cluttered
- Rectangle is too small (< 2% of image area)
- Edges are not clear enough

**Solutions:**
- Try adjusting the image (crop, increase contrast)
- Use manual positioning
- Adjust `QuadDetectionMinArea` constant in code

### Wrong Shape Selected
**Solutions:**
- Choose a different shape from the dialog
- Click "Set Manually" and position corners yourself
- Close other objects in the frame before capturing
- Ensure better lighting when taking the photograph

### Detection is Slow
**Possible causes:**
- Very large image (> 10 megapixels)
- Complex scene with many contours

**Solutions:**
- Resize image before processing
- Crop to area of interest
- Close other running applications

## Credits

### Libraries Used
- **OpenCV** - Computer vision algorithms
- **Emgu.CV** - .NET wrapper for OpenCV
- **ImageMagick** - Image processing and transformation
- **WPF-UI** - Modern UI controls

### References
- OpenCV documentation: https://docs.opencv.org/
- Emgu.CV documentation: http://www.emgu.com/wiki/
- Canny edge detection: https://en.wikipedia.org/wiki/Canny_edge_detector
- Contour detection: https://docs.opencv.org/4.x/d3/dc0/group__imgproc__shape.html
