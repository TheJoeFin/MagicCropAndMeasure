# MagickCrop MVVM Migration - Step 19 Testing Checklist

This document provides a comprehensive testing checklist for verifying the complete functionality of MagickCrop after the MVVM migration. These correspond to Steps 19d-19k of the migration guide.

## Testing Overview

After the MVVM migration, MagickCrop maintains all original functionality with improved architecture. This checklist verifies that all features work correctly.

**Note:** Testing should be performed in sequence, as some features depend on earlier setup.

---

## Step 19d: Test Image Loading

### File Loading
- [ ] **Load image from File → Open Image**
  - [ ] Opens Windows file dialog
  - [ ] Can navigate to image files
  - [ ] Supports common formats (JPG, PNG, BMP, TIFF, WebP)
  - [ ] Image displays correctly in canvas
  - [ ] Image metadata shown (dimensions, file size)
  - [ ] Recent projects list updated

### Drag and Drop Loading
- [ ] **Drag image file to main window**
  - [ ] Image loads successfully
  - [ ] Image displays with correct aspect ratio
  - [ ] No errors or exceptions

### Clipboard Paste Loading
- [ ] **Edit → Paste Image (or Ctrl+V)**
  - [ ] Clipboard image pastes into application
  - [ ] Image displays correctly
  - [ ] Image dimensions shown
  - [ ] Works after copying image from external application
  - [ ] Works after copying screenshot

### Image Load Error Handling
- [ ] **Try loading corrupted file**
  - [ ] Shows user-friendly error message
  - [ ] Doesn't crash application
  - [ ] Can still load valid image afterward

- [ ] **Try loading unsupported format**
  - [ ] Shows error message
  - [ ] Application remains stable

---

## Step 19e: Test Image Operations

### Rotation Operations
- [ ] **Image → Rotate Clockwise (90°)**
  - [ ] Image rotates correctly
  - [ ] Rotation persists visually
  - [ ] Canvas updates properly
  - [ ] Can rotate multiple times

- [ ] **Image → Rotate Counter-Clockwise (90°)**
  - [ ] Image rotates counter-clockwise
  - [ ] Multiple rotations work correctly
  - [ ] Can undo/redo rotations

### Flip Operations
- [ ] **Image → Flip Horizontal**
  - [ ] Image flips horizontally
  - [ ] Effect is reversible with second flip

- [ ] **Image → Flip Vertical**
  - [ ] Image flips vertically
  - [ ] Effect is reversible with second flip

### Crop Operations
- [ ] **Tools → Crop Tool / Select crop area**
  - [ ] Can select rectangular area on image
  - [ ] Crop preview shows correctly
  - [ ] Apply crop removes areas outside selection
  - [ ] Image dimensions update after crop

### Color and Contrast
- [ ] **Image → Adjust → Color/Contrast**
  - [ ] Dialog opens with sliders
  - [ ] Adjustments preview on image
  - [ ] Changes apply when confirmed
  - [ ] Can undo adjustments

### Undo/Redo
- [ ] **Edit → Undo after operations**
  - [ ] Undoes last operation
  - [ ] Can undo multiple times
  - [ ] Reverts to correct state

- [ ] **Edit → Redo after Undo**
  - [ ] Redoes previously undone operations
  - [ ] Maintains correct state

---

## Step 19f: Test Each Measurement Type

### Distance Measurement
- [ ] **Measurement → Distance Tool**
  - [ ] Tool activates
  - [ ] Can click two points on image
  - [ ] Distance line displays between points
  - [ ] Distance value shown (pixels and real-world units)
  - [ ] Can drag points to modify measurement
  - [ ] Can delete measurement with Delete key or UI button

### Angle Measurement
- [ ] **Measurement → Angle Tool**
  - [ ] Tool activates
  - [ ] Can click three points
  - [ ] Angle arc displays
  - [ ] Angle value shown in degrees
  - [ ] Can modify points by dragging
  - [ ] Can delete angle measurement

### Rectangle Measurement
- [ ] **Measurement → Rectangle Tool**
  - [ ] Tool activates
  - [ ] Can click four corner points
  - [ ] Rectangle outline displays
  - [ ] Width, height, area displayed
  - [ ] Can drag corners to resize
  - [ ] Can delete measurement

### Circle Measurement
- [ ] **Measurement → Circle Tool**
  - [ ] Tool activates
  - [ ] Can click center point
  - [ ] Can click edge point or drag to set radius
  - [ ] Circle displays with center and radius
  - [ ] Radius/diameter values shown
  - [ ] Can modify by dragging
  - [ ] Can delete measurement

### Polygon Measurement
- [ ] **Measurement → Polygon Tool**
  - [ ] Tool activates
  - [ ] Can click multiple points to form polygon
  - [ ] Points connect with lines
  - [ ] Right-click or double-click completes polygon
  - [ ] Perimeter and area displayed
  - [ ] Can drag vertices to modify
  - [ ] Can delete measurement

### Line Guides
- [ ] **Measurement → Horizontal Line Guide**
  - [ ] Guide line displays horizontally
  - [ ] Can drag to adjust position
  - [ ] Position value shown
  - [ ] Can delete guide

- [ ] **Measurement → Vertical Line Guide**
  - [ ] Guide line displays vertically
  - [ ] Can drag to adjust position
  - [ ] Position value shown
  - [ ] Can delete guide

### Measurement Units
- [ ] **Right-click measurement to set real-world units**
  - [ ] Unit dialog appears
  - [ ] Can set pixels-to-inches/cm/mm conversion
  - [ ] Measurement values update to show real-world units
  - [ ] All measurements update when scale changes

- [ ] **View → Measurement Units**
  - [ ] Can change display units globally
  - [ ] All measurements update
  - [ ] Persists across sessions

---

## Step 19g: Test Save/Load Cycle

### Save Project
- [ ] **File → Save Project**
  - [ ] Save dialog appears for first save
  - [ ] Default location is reasonable
  - [ ] Can specify filename
  - [ ] File saved with .mcm extension
  - [ ] Window title shows project name

- [ ] **File → Save Project (after save)**
  - [ ] Saves to existing location
  - [ ] No dialog appears

- [ ] **File → Save Project As**
  - [ ] Dialog appears
  - [ ] Can choose new location
  - [ ] Creates new file
  - [ ] Window title updates

### Load Project
- [ ] **File → Open Project (.mcm file)**
  - [ ] File dialog shows
  - [ ] Can select .mcm file
  - [ ] Project loads completely
  - [ ] Image appears
  - [ ] All measurements restore correctly
  - [ ] Undo/redo history preserved

- [ ] **Double-click .mcm file in Explorer**
  - [ ] Application launches
  - [ ] Project loads automatically
  - [ ] Image and measurements visible

### Auto-save
- [ ] **Make changes without manual save**
  - [ ] Application periodically auto-saves
  - [ ] Auto-save location is accessible
  - [ ] Can recover from auto-save if needed

### Recent Projects
- [ ] **File → Recent Projects**
  - [ ] List shows recently opened projects
  - [ ] Clicking project opens it
  - [ ] Thumbnails display for recent projects

- [ ] **Welcome Screen**
  - [ ] Shows recent projects
  - [ ] Thumbnails are visible
  - [ ] Can open project by clicking thumbnail
  - [ ] Can clear recent projects history

---

## Step 19h: Test Recent Projects Feature

### Recent Projects Display
- [ ] **Recent projects appear in File menu**
  - [ ] List shows last N projects
  - [ ] Projects appear in correct order (most recent first)

- [ ] **Welcome screen displays recent projects**
  - [ ] Thumbnails show project images
  - [ ] Project names are readable
  - [ ] Hover shows full path

### Opening Recent Projects
- [ ] **Click recent project in File menu**
  - [ ] Project opens correctly
  - [ ] Image displays
  - [ ] Measurements restore

- [ ] **Click recent project thumbnail on Welcome screen**
  - [ ] Project opens
  - [ ] Full image displayed
  - [ ] All measurements present

### Recent Projects Management
- [ ] **Project appears in recent list after save**
  - [ ] New projects added to recent list
  - [ ] Duplicate projects don't create duplicates in list

- [ ] **Recent projects persist across sessions**
  - [ ] Close and reopen application
  - [ ] Recent projects still in list

- [ ] **Clear Recent Projects**
  - [ ] File → Clear Recent History option works
  - [ ] Recent list cleared
  - [ ] Welcome screen shows "No recent projects"

---

## Step 19i: Test Keyboard Shortcuts

### File Operations
- [ ] **Ctrl+O** - Open image
  - [ ] Opens file dialog
  
- [ ] **Ctrl+S** - Save project
  - [ ] Saves current project
  
- [ ] **Ctrl+Shift+S** - Save As
  - [ ] Opens save dialog with new filename

- [ ] **Ctrl+E** - Export image
  - [ ] Opens export dialog

### Edit Operations
- [ ] **Ctrl+Z** - Undo
  - [ ] Undoes last operation
  
- [ ] **Ctrl+Y** - Redo
  - [ ] Redoes undone operation
  
- [ ] **Ctrl+V** - Paste image
  - [ ] Pastes from clipboard
  
- [ ] **Ctrl+A** - Select all (if applicable)
  - [ ] Works as expected

### View Operations
- [ ] **Ctrl++ (Plus)** - Zoom in
  - [ ] Image zooms in
  
- [ ] **Ctrl+- (Minus)** - Zoom out
  - [ ] Image zooms out
  
- [ ] **Ctrl+0** - Fit to window
  - [ ] Resets zoom to fit

### Measurement Operations
- [ ] **Delete key** - Delete selected measurement
  - [ ] Removes measurement
  
- [ ] **Escape** - Cancel current operation
  - [ ] Cancels measurement creation

### Window Operations
- [ ] **Alt+F4** - Close window
  - [ ] Closes application properly
  
- [ ] **F1** - Help
  - [ ] Opens help if available

---

## Step 19j: Test Undo/Redo Functionality

### Undo Operations
- [ ] **Undo image rotation**
  - [ ] Image returns to previous rotation state
  
- [ ] **Undo image flip**
  - [ ] Flip is reversed
  
- [ ] **Undo crop**
  - [ ] Image returns to pre-crop state
  
- [ ] **Undo measurement addition**
  - [ ] Measurement is removed
  
- [ ] **Undo measurement modification**
  - [ ] Measurement returns to previous position/value

### Redo Operations
- [ ] **Redo after Undo**
  - [ ] Reapplies undone operation
  
- [ ] **Multiple redo operations**
  - [ ] Can redo multiple steps in sequence
  
- [ ] **Redo clears when new operation performed**
  - [ ] After making new change, redo history is cleared

### Undo Stack Behavior
- [ ] **Undo multiple operations**
  - [ ] Can undo 10+ operations
  - [ ] Each undo returns to correct state
  
- [ ] **Undo all the way to empty state**
  - [ ] Returns to start state if all operations undone
  
- [ ] **Redo all operations**
  - [ ] Can redo back to current state

---

## Step 19k: Test Export Functionality

### Export Image
- [ ] **File → Export Image**
  - [ ] Export dialog appears
  - [ ] Can choose location and filename

### Export Formats
- [ ] **Export as JPEG**
  - [ ] File saves as .jpg
  - [ ] Quality setting available
  - [ ] Image visible after export
  
- [ ] **Export as PNG**
  - [ ] File saves as .png
  - [ ] Image maintains quality
  
- [ ] **Export as BMP**
  - [ ] File saves as .bmp
  
- [ ] **Export as TIFF**
  - [ ] File saves as .tiff
  
- [ ] **Export as WebP**
  - [ ] File saves as .webp

### Export Options
- [ ] **Image quality slider** (for JPEG/WebP)
  - [ ] Slider adjusts quality
  - [ ] Lower values reduce file size
  - [ ] Higher values increase quality

- [ ] **Resize option**
  - [ ] Can specify output dimensions
  - [ ] Can maintain aspect ratio
  - [ ] Can specify by percentage or pixels

- [ ] **Preview**
  - [ ] Export preview shows image
  - [ ] Shows final dimensions
  - [ ] Shows estimated file size

### Save/Copy Functionality
- [ ] **Copy to Clipboard**
  - [ ] Copies processed image to clipboard
  - [ ] Can paste into other applications

- [ ] **Open File Location**
  - [ ] Opens folder containing exported file
  - [ ] File is visible in file explorer

---

## Integration Tests

### Full Workflow
- [ ] **Complete workflow test:**
  1. [ ] Load image from file
  2. [ ] Rotate image
  3. [ ] Add distance measurement
  4. [ ] Add angle measurement
  5. [ ] Add rectangle measurement
  6. [ ] Set real-world units
  7. [ ] Save as project
  8. [ ] Close application
  9. [ ] Reopen application
  10. [ ] Open recent project
  11. [ ] Verify all measurements and settings preserved
  12. [ ] Export image
  13. [ ] Verify exported file exists and displays correctly

### Error Recovery
- [ ] **Application stability under error conditions**
  - [ ] Load invalid file - shows error, remains stable
  - [ ] Open corrupted project file - shows error, allows recovery
  - [ ] Fill disk during save - shows error, doesn't crash

### Performance
- [ ] **Load large image (10MB+)**
  - [ ] Application doesn't freeze
  - [ ] Progress indicators shown if needed
  - [ ] Image eventually loads

- [ ] **Many measurements on image (50+)**
  - [ ] Application remains responsive
  - [ ] No lag when adding/moving measurements

---

## Testing Notes

### Expected Behavior
- All features should work as they did before the MVVM migration
- Application should be responsive and never freeze
- User should not see any exceptions or error dialogs (except for legitimate errors)
- Undo/redo history should persist within a session
- Recent projects should persist across sessions

### Known Limitations
- Undo/redo history is cleared when project is saved
- Auto-save occurs periodically, not after every operation
- Some complex measurements may have slight display updates lag on very slow systems

### Test Environment
- **OS:** Windows 10 or Windows 11
- **Screen Res:** 1920x1080 or similar
- **Image Samples:** Include various formats and sizes
  - Small image: 100x100 pixels
  - Medium image: 2000x1500 pixels
  - Large image: 8000x6000 pixels

### Reporting Issues
When reporting testing issues, include:
1. Operating System and version
2. Image file (format, size)
3. Steps to reproduce
4. Expected vs. actual behavior
5. Any error messages or exceptions
6. Screenshot if applicable

---

## Completion Criteria

✅ **All tests passed** if:
- All checkboxes above are checked
- No crashes or unhandled exceptions
- All functionality works as expected
- Application is responsive and stable
- Data persists correctly across save/load cycles

## Sign-off

- **Tested by:** [Your name]
- **Date:** [Date]
- **Result:** ✅ PASS / ❌ FAIL
- **Notes:** [Any issues found or notes about testing]
