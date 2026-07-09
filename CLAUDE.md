# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Magick Crop & Measure is a WPF desktop app (Windows 10+) that corrects perspective distortion in photos and provides measurement tools. It uses ImageMagick for image processing, Emgu.CV (OpenCV wrapper) for shape detection, and WPF-UI (Fluent Design) for the UI.

## Build Commands

```bash
# Build the solution (from repo root)
dotnet build MagickCrop.sln

# Build a specific platform (arm64, x64, x86)
dotnet build MagickCrop/MagickCrop.csproj -p:Platform=x64

# Run (requires Windows with .NET 10)
dotnet run --project MagickCrop/MagickCrop.csproj

# Run tests
dotnet test MagickCrop.Tests/

# Publish self-contained for x64
dotnet publish MagickCrop/MagickCrop.csproj -r win-x64 --self-contained -o bld/x64/MagickCrop-Self-Contained
```

The MSIX packaging project (`MagickCrop-Package/MagickCrop-Package.wapproj`) is used for Store submissions and requires Visual Studio.

## Architecture

### Key Namespaces

- `MagickCrop` — root namespace; `MainWindow.xaml.cs` and `MainWindow.QuadrilateralHover.cs` are partial classes for the main window
- `MagickCrop.ViewModels` — `MainWindowViewModel` (CommunityToolkit.Mvvm `ObservableObject`) + `IMainWindowView` interface
- `MagickCrop.Helpers` — stateless static helpers for image processing operations
- `MagickCrop.Controls` — WPF `UserControl` subclasses for measurement overlays and interactive controls
- `MagickCrop.Models` — DTOs, `UndoRedo` stack, `AspectRatio`, `DraggingMode` enum
- `MagickCrop.Models.MeasurementControls` — serializable DTOs for each measurement type
- `MagickCrop.Services` — `RecentProjectsManager` (JSON-backed project persistence in `%LocalAppData%\MagickCrop`)
- `MagickCrop.Behaviors` — `PinchZoomBehavior` attached property

### View / ViewModel Split

`MainWindow` implements `IMainWindowView`, which exposes just the properties the ViewModel needs (image source, busy state, local-adjustment region). `MainWindowViewModel` holds all commands (CommunityToolkit source-generated `[RelayCommand]`) and observable state. UI-only logic (dragging handles, polygon rendering) stays in the code-behind.

### Image Processing Pipeline

All image mutations write to a **temp file** on disk and reload from it. Undo/redo (`UndoRedo` + `MagickImageUndoRedoItem`) stores before/after file paths and reloads from disk on undo/redo — there is no in-memory image stack.

Key helpers:
- `QuadrilateralDetector` — Emgu.CV-based contour detection, returns `DetectedQuadrilateral` with confidence score
- `GridStraightenHelper` — polynomial distortion via ImageMagick for grid-based warp correction
- `UnWarpCorrector` — barrel/pincushion correction using transfinite interpolation + polynomial distortion
- `EdgeCorrectionHelper` — wavy-edge straightening using the same transfinite interpolation approach
- `ImageExtensions` / `MagickExtensions` — extension methods bridging WPF types and Magick.NET types

### Measurement Controls

Each measurement type (Distance, Angle, Rectangle, Polygon, Circle, VerticalLine, HorizontalLine) is a `UserControl` with a corresponding `*Dto` model for JSON serialization. Controls live on a WPF `Canvas` overlaid on the image.

### Project Persistence

`RecentProjectsManager` saves projects as JSON files (including measurement state as `MagickCropMeasurementPackage`) with thumbnails under `%LocalAppData%\MagickCrop\Projects`. An auto-save timer fires every 5 seconds when a project is open.

### UI Framework

Uses **WPF-UI** (`Wpf.Ui`) for `FluentWindow`, theming, and Fluent controls. `MainWindow` extends `FluentWindow`. Theme follows system (light/dark) via `ApplicationThemeManager`.
