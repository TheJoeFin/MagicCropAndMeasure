# MagickCrop Issues & Feature Requests

This document tracks known issues, feature requests, and work items for MagickCrop.

---

## ✅ RESOLVED ISSUES

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

### Enhancement: Dark Mode
- **Description:** Add dark theme support using WPF-UI themes
- **Priority:** Low
- **Estimated Effort:** 3-4 hours
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
- **Build Status:** 0 errors, 4 non-critical warnings (pre-existing NuGet issues)
- **Last Updated:** January 18, 2026
- **Next Review:** Upon completion of first new feature/enhancement

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
