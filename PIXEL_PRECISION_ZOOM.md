# Pixel Precision Zoom Feature

## Overview

The Pixel Precision Zoom feature provides a magnified preview of the image at the cursor position, enabling precise point placement when working with measurements and image transformations. This feature is similar to the zoom functionality found in professional tools like PowerToys Color Picker and Adobe Photoshop.

## Visual Appearance

The zoom control appears as a **circular preview window** with:
- **6x magnification** of the image region under the cursor
- **Blue border** matching the application's accent color (#0066FF)
- **Red crosshair** overlay indicating the exact target pixel
- **Coordinate display** showing the current pixel position (X, Y)
- **150x150 pixel** preview window size

## When Does It Appear?

The pixel zoom preview automatically appears when you:

### 1. Transform Corners
- **Dragging corner markers** (TopLeft, TopRight, BottomLeft, BottomRight) for perspective transformation
- The zoom preview helps you precisely align corners with document edges or specific features

### 2. Creating Measurements
- **Distance measurements** - When placing start and end points
- **Angle measurements** - When placing the three points (two legs and vertex)
- **Rectangle measurements** - When defining the rectangle corners
- **Circle measurements** - When setting the center and edge points
- **Polygon measurements** - When placing polygon vertices

### 3. Adjusting Existing Measurements
- **Moving measurement points** - When dragging any point on an existing measurement to adjust its position

## How to Use

### Basic Usage
1. Start any operation that requires point placement (transform, measurement, etc.)
2. Click and hold the left mouse button on a point
3. The zoom preview automatically appears near your cursor
4. Observe the magnified view and crosshair to see exactly which pixel you're targeting
5. Move the mouse to position the point precisely
6. Release the mouse button to finalize the placement
7. The zoom preview automatically disappears

### Smart Positioning
The zoom preview intelligently positions itself:
- **Default position**: Top-left of the cursor (offset by 40 pixels)
- **Adaptive positioning**: If there's not enough space at the default position, it moves to avoid being cut off
- **Non-blocking**: The preview never blocks your view of the point you're placing

### Coordinate Display
The pixel coordinates at the bottom of the zoom window show:
- **X coordinate**: Horizontal position in the image (0 = left edge)
- **Y coordinate**: Vertical position in the image (0 = top edge)
- **Units**: Always in pixels, regardless of measurement unit settings

## Technical Details

### Implementation
- **Control Type**: Custom WPF UserControl (`PixelPrecisionZoom`)
- **Image Processing**: Uses `CroppedBitmap` and `TransformedBitmap` for efficient rendering
- **Coordinate Conversion**: Automatically converts between canvas and image pixel coordinates
- **Performance**: Lightweight and responsive, even with large images

### Zoom Calculation
The zoom preview captures a region around the cursor:
- **Capture region**: 25x25 pixels (150 ÷ 6 zoom factor)
- **Output size**: 150x150 pixels
- **Magnification**: 6x zoom level

### Integration Points
The feature is integrated into:
- `MainWindow.xaml` - Added to the ShapeCanvas overlay
- `MainWindow.xaml.cs` - Mouse event handlers:
  - `TopLeft_MouseDown` - Shows zoom when dragging transform corners
  - `TopLeft_MouseMove` - Updates zoom position in real-time
  - `ShapeCanvas_MouseUp` - Hides zoom when mouse is released
  - `MeasurementPoint_MouseDown` - Shows zoom for all measurement types
  - `ShapeCanvas_MouseDown` - Shows zoom when creating new measurements

## Benefits

### Precision
- **Pixel-perfect placement**: See exactly which pixel you're targeting
- **Visual feedback**: Real-time preview as you move the cursor
- **Coordinate reference**: Know the exact pixel position

### Usability
- **Automatic operation**: No manual activation required
- **Non-intrusive**: Only appears when needed
- **Professional workflow**: Similar to industry-standard tools

### Accuracy
- **6x magnification**: Sufficient zoom to see individual pixels clearly
- **Crosshair guide**: Precise targeting with visual indicator
- **Stable preview**: Follows cursor smoothly without lag

## Future Enhancements (Possible)

The current implementation could be extended with:
- **Configurable zoom level**: Allow users to adjust magnification (4x, 8x, 10x, etc.)
- **Adjustable preview size**: Different window sizes for different preferences
- **Toggle on/off**: Option to disable the feature if not needed
- **Keyboard shortcut**: Hold a key to temporarily show/hide the zoom
- **Color information**: Display the RGB values of the pixel under the crosshair
- **Grid overlay**: Show a pixel grid in the zoomed view

## Code Structure

### Files
- `MagickCrop/Controls/PixelPrecisionZoom.xaml` - UI definition
- `MagickCrop/Controls/PixelPrecisionZoom.xaml.cs` - Control logic
- `MagickCrop/MainWindow.xaml` - Integration into main window
- `MagickCrop/MainWindow.xaml.cs` - Mouse event handling

### Key Methods
- `ShowPixelZoom(Point)` - Displays the zoom control
- `UpdatePixelZoom(Point)` - Updates zoom position and content
- `HidePixelZoom()` - Hides the zoom control
- `ConvertCanvasToImageCoordinates(Point)` - Coordinate conversion
- `ShouldShowPixelZoom()` - Determines when to show zoom

## Troubleshooting

### Zoom doesn't appear
- Ensure an image is loaded
- Check that you're performing an operation that supports zoom (dragging a point)
- Verify the mouse button is held down

### Zoom shows wrong region
- This may occur if the image has complex transformations
- The coordinate conversion handles most cases automatically

### Performance issues
- Very large images (>10000x10000 pixels) may cause slight delays
- The implementation is optimized for typical photo sizes

## Conclusion

The Pixel Precision Zoom feature enhances MagickCrop's precision capabilities, making it easier to:
- Align transformation corners with document edges
- Place measurement points at specific features
- Achieve pixel-perfect accuracy in all operations

This professional-grade feature brings MagickCrop closer to the precision tools found in industry-standard applications while maintaining the application's ease of use and performance.
