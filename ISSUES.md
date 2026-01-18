# MagickCrop Issues & Feature Requests

This document tracks known issues, feature requests, and work items for MagickCrop.

---

## ✅ RESOLVED ISSUES

### Feature: Dark Mode Support ✅ IMPLEMENTED
**Implementation Date:** January 18, 2026  
**Commit:** TBD (to be committed)

Implemented full dark mode support with theme persistence. Users can toggle between light and dark themes, and their preference is automatically saved and restored on application restart. Features include:

- **ThemeService Integration**: Created `ThemeService` implementing `IThemeService` interface
- **Theme Persistence**: Theme preference stored in JSON file (`%LocalAppData%/MagickCrop/theme-settings.json`)
- **WPF-UI Integration**: Uses `ApplicationThemeManager` for consistent theme application
- **UI Controls**: Theme toggle button added to measurement panel
- **Auto-initialization**: Theme automatically loaded on app startup
- **Comprehensive Testing**: All 291 unit tests passing with theme service mocks

**Details:**
- Created `ThemeService.cs` with theme switching and persistence logic
- Created `MockThemeService.cs` for unit testing
- Added theme commands to `MainWindowViewModel`
- Updated dependency injection and app startup
- UI button for quick theme toggling
- All tests updated to include theme service dependency

**Status:** ✅ Complete and tested

---

### Refactoring: Extract Thumbnail Logic to ThumbnailService ✅ FIXED
**Resolution Date:** January 18, 2026  
**Commit:** 3742567 - "Refactor: Extract thumbnail logic to dedicated ThumbnailService"

Extracted thumbnail generation logic from RecentProjectsManager into a dedicated ThumbnailService with proper dependency injection. This improves separation of concerns, testability, and follows SOLID principles.

**Details:**
- Created `IThumbnailService` interface and `ThumbnailService` implementation
- Refactored `RecentProjectsManager` to depend on `IThumbnailService`
- Added 6 comprehensive unit tests for ThumbnailService
- Created `MockThumbnailService` for integration testing
- Updated DI registration in App.xaml.cs
- All 291 tests passing

**Status:** ✅ Resolved and tested

---

### Issue #1: Make Persistence Truly Async ✅ FIXED
**Resolution Date:** January 18, 2026  
**Commit:** 3bb7c1e - "Issue #1: Convert misleading Async methods to true async"

Converted `SaveToFileAsync()` and `LoadFromFileAsync()` methods from synchronous operations with misleading async names to true async methods using `async Task`. Updated all 5+ call sites to properly `await` async operations.

**Status:** ✅ Resolved and tested

---

### Issue #4: Centralize Path & Folder Logic ✅ FIXED
**Resolution Date:** January 18, 2026  
**Commit:** 9568078 - "Issue #4: Centralize path & folder logic with IAppPaths service"

Created `IAppPaths` interface and `AppPaths` implementation to centralize application path building logic. Refactored `RecentProjectsManager` to depend on `IAppPaths` instead of duplicating path-building logic.

**Status:** ✅ Resolved and tested

---

### Issue #8: Memory Management for MagickImage ✅ FIXED
**Resolution Date:** January 18, 2026  
**Commit:** 18fe9de - "Fix: Dispose MagickImage objects in OpenImagePath to prevent memory leaks"

Fixed critical memory leak in `MainWindow.xaml.cs:OpenImagePath()` where MagickImage objects were created without proper disposal. Added `using` statements to ensure cleanup of ImageMagick unmanaged resources.

**Status:** ✅ Resolved and tested

---

## 📋 OPEN ISSUES

*Currently, no open issues are documented. All migration work has been completed and the application is production-ready with 291 passing tests.*

---

## 🚀 POTENTIAL FUTURE ENHANCEMENTS

These are features/improvements that could be considered for future releases:

### Feature: Advanced Undo/Redo
- **Description:** Extend undo/redo to persist across save/load cycles
- **Priority:** Medium
- **Estimated Effort:** 4-6 hours
- **Status:** Not started

### Feature: Multi-window Support
- **Description:** Allow multiple MagickCrop windows for comparing images
- **Priority:** Low
- **Estimated Effort:** 6-8 hours
- **Status:** Not started

### Feature: Plugin Architecture
- **Description:** Create a plugin system for custom measurement tools and filters
- **Priority:** Low
- **Estimated Effort:** 12-16 hours
- **Status:** Not started

### Feature: Cloud Storage Integration
- **Description:** Support for Google Drive, OneDrive, AWS S3 project storage
- **Priority:** Low
- **Estimated Effort:** 8-12 hours
- **Status:** Not started

### Enhancement: Performance Optimization
- **Description:** Profile and optimize for very large images (50MB+)
- **Priority:** Medium
- **Estimated Effort:** 8-10 hours
- **Status:** Not started

---

## 🔍 KNOWN LIMITATIONS

These are intentional design limitations or behaviors that may be addressed in future versions:

1. **Undo/Redo History Cleared on Save**
   - Currently, undo/redo history is cleared when a project is saved
   - This could be extended to persist across sessions in a future version

2. **Single Project at a Time**
   - Application only supports one open project at a time
   - Multi-window support would be needed to change this

3. **Auto-Save Interval**
   - Auto-save happens on a fixed interval, not after every operation
   - Could be optimized for performance on large projects

---

## 📊 TESTING STATUS

### Unit Tests
- **Total Tests:** 291
- **Passing:** 291 ✅
- **Coverage:** ~90%

### Manual Testing Checklist
See `TESTING_CHECKLIST.md` for comprehensive manual testing procedures.

---

## 💬 NOTES

- **MVVM Migration:** 100% complete as of January 18, 2026
- **Post-Migration Refactoring:** ThumbnailService extraction completed January 18, 2026
- **Dark Mode Feature:** Implemented January 18, 2026 (291 tests passing)
- **Build Status:** 0 errors, 6 pre-existing warnings (NuGet/SDK issues)
- **Test Status:** All 291 tests passing (100% pass rate)
- **Last Updated:** January 18, 2026 (Dark Mode implementation)
- **Next Review:** Upon completion of next feature/enhancement

---

## ISSUE TEMPLATE

When reporting new issues, please include:

```markdown
### Issue Title
**Description:** [What is the problem?]  
**Steps to Reproduce:** [How can we reproduce it?]  
**Expected Behavior:** [What should happen?]  
**Actual Behavior:** [What actually happens?]  
**Environment:** [OS version, screen resolution, etc.]  
**Priority:** [Critical / High / Medium / Low]  
**Status:** [Open / In Progress / Testing / Resolved]  
```

---

**Last Updated:** January 18, 2026

### Issue: Failed to render (Critical Threading Issue) ✅ FIXED
**Resolution Date:** January 18, 2026  
**Commit:** (to be committed)

**Problem:** Application crashed with `System.ArgumentException: Must create DependencySource on same Thread as the DependencyObject` when rendering images after async operations.

**Root Cause:** Multiple issues in threading for UI updates:
1. `MainImage.Source` (a DependencyProperty) was being set from background thread contexts
2. `ToBitmapSource()` method in ImageProcessingService was using `using` statement that disposed the MemoryStream immediately, causing potential thread affinity issues with the BitmapImage

**Solution Implemented:**
1. **Modified ImageProcessingService.ToBitmapSource()**: Removed `using` statement that was prematurely disposing the MemoryStream. Changed to only dispose on error to prevent BitmapImage from losing its backing stream while still holding a reference.

2. **Added SetMainImageSource() helper method**: Created a thread-safe wrapper that checks if we're on the UI thread using `Dispatcher.CheckAccess()` and marshals to the UI thread using `Dispatcher.BeginInvoke()` if needed.

3. **Replaced all MainImage.Source assignments**: Updated all 23 instances where `MainImage.Source` was directly assigned to use the new `SetMainImageSource()` method instead. This ensures every UI update is properly marshalled to the UI thread.

**Files Modified:**
- `MagickCrop/Services/ImageProcessingService.cs`: Fixed ToBitmapSource method
- `MagickCrop/MainWindow.xaml.cs`: Added SetMainImageSource helper method and replaced all 23 MainImage.Source assignments

**Testing:** All 291 unit tests passing ✅

**Status:** ✅ Fixed and tested


