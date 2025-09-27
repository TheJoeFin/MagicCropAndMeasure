# Fix for Crop Bounds Mismatch After Distortion Correction

## Issue Description

Users reported that crop bounds do not match where the cropping overlay is placed after perspective distortion correction is applied. This happens because:

1. **Before Distortion**: The cropping rectangle overlay is positioned correctly relative to the original (pre-correction) image display
2. **After Distortion**: The image dimensions change due to perspective correction, but the crop rectangle overlay stays in the same screen position
3. **Result**: The crop rectangle now corresponds to incorrect image coordinates

## Root Cause

The `CorrectDistortion()` method uses `Bestfit = true` when applying perspective distortion, which can change the output image dimensions. When the corrected image replaces the original image as `MainImage.Source`, the cropping rectangle overlay (`CroppingRectangle`) is not updated to match the new image coordinates.

## Solution

Modified the `ApplyButton_Click()` method in `MainWindow.xaml.cs` to:

1. **Capture Original State**: Before applying distortion correction, capture:
   - Original image display size (`MainImage.ActualWidth/Height`)
   - Cropping rectangle position and size if visible
   
2. **Apply Distortion Correction**: Perform the existing distortion correction logic

3. **Adjust Crop Rectangle**: After updating `MainImage.Source` with the corrected image:
   - Force layout update with `UpdateLayout()` to ensure new dimensions are available
   - Calculate scale factors between old and new display sizes
   - Transform crop rectangle position and size using the scale factors
   - Apply bounds checking to keep the rectangle within the new image bounds
   - Update the crop rectangle's position and size

## Code Changes

The fix is implemented in the `ApplyButton_Click()` method around lines 535-609:

```csharp
// Before distortion - capture original state
Size originalDisplaySize = new(MainImage.ActualWidth, MainImage.ActualHeight);
bool cropRectangleVisible = CroppingRectangle.Visibility == Visibility.Visible;
// ... capture crop rectangle coordinates

// Apply distortion correction
MagickImage? image = await CorrectDistortion(imagePath);
MainImage.Source = image.ToBitmapSource();

// After distortion - adjust crop rectangle if it was visible
if (cropRectangleVisible)
{
    UpdateLayout(); // Ensure new display dimensions are available
    Size newDisplaySize = new(MainImage.ActualWidth, MainImage.ActualHeight);
    
    // Calculate and apply transformations
    double widthScale = newDisplaySize.Width / originalDisplaySize.Width;
    double heightScale = newDisplaySize.Height / originalDisplaySize.Height;
    // ... transform and apply new coordinates
}
```

## Benefits

- **Accurate Cropping**: Crop rectangles now correctly correspond to the intended image area after distortion correction
- **User Experience**: Users no longer need to readjust crop rectangles after applying perspective correction
- **Backward Compatible**: No changes to file formats or existing functionality
- **Robust**: Includes bounds checking and error handling for edge cases

## Testing Considerations

To test this fix:

1. Load an image with perspective distortion
2. Place a cropping rectangle over a specific area
3. Apply perspective distortion correction
4. Verify the cropping rectangle still covers the same visual area in the corrected image
5. Apply the crop and verify the correct portion of the image is cropped

The fix handles various edge cases:
- Different aspect ratio changes after correction
- Crop rectangles near image boundaries
- Images that significantly change size during correction