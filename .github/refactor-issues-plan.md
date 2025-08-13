# Refactor & Cleanup Issue Plan

This document enumerates proposed GitHub issues derived from the refactor / simplification plan.
Each section can be copied into a separate GitHub issue. Suggested labels: `refactor`, `tech-debt`, `enhancement`, `performance`, `bug`.

---
## 1. Make Persistence Truly Async (or Rename Methods)
**Problem**: Methods like `SaveToFileAsync` return `bool` synchronously; misleading naming.
**Actions**:
- Inspect all *Async suffix methods.
- Convert to `async Task` / `Task<T>` using `await` on I/O OR rename (drop Async).
- Update call sites.
**Acceptance**:
- No synchronous *Async methods remain.
**Labels**: refactor, consistency

## 2. RecentProjectsManager ObservableCollection Replacement
**Problem**: Reassigning `RecentProjects = [.. projects];` breaks existing WPF bindings.
**Actions**:
- Keep a single instance; use `Clear()` + `Add()`.
- Audit UI for binding dependencies.
**Acceptance**:
- No reassignment of the ObservableCollection instance.
**Labels**: bug, refactor

## 3. Introduce Service Abstractions (DI Ready)
**Problem**: Tight coupling & hard to test.
**Actions**:
- Add interfaces: `IRecentProjectsManager`, `IAppPaths`, `IThumbnailService`, `IProjectPersistenceService`, `IJsonSerializer`.
- Provide concrete implementations & lightweight composition (optionally `Microsoft.Extensions.DependencyInjection`).
**Acceptance**:
- Core services accessed via interfaces.
**Labels**: refactor, architecture

## 4. Centralize Path & Folder Logic
**Problem**: Path building logic duplicated inside `RecentProjectsManager`.
**Actions**:
- Implement `AppPaths` service for root, projects, thumbnails, index file.
- Ensure directory creation handled once.
**Acceptance**:
- No direct environment path logic inside feature classes.
**Labels**: refactor

## 5. Extract Thumbnail Logic
**Problem**: Thumbnail generation mixed with project management.
**Actions**:
- Create `ThumbnailService` (`CreateAsync(BitmapSource, id, width = 200)`).
- Move scaling & encoding there.
**Acceptance**:
- `RecentProjectsManager` delegates to `IThumbnailService`.
**Labels**: refactor, separation-of-concerns

## 6. Add Shared Json Serialization Options
**Problem**: Direct `JsonSerializer` calls without shared settings.
**Actions**:
- Create `JsonSerializationOptions` static or `IJsonSerializer` wrapper.
- Centralize naming policy / indentation / ignore nulls.
**Acceptance**:
- All serialization uses shared options.
**Labels**: refactor, consistency

## 7. Improve Error Handling & Logging
**Problem**: Silent catch blocks swallow exceptions.
**Actions**:
- Introduce `ILogger` abstraction (or simple static Logger).
- Replace empty catches with log calls & optional user-friendly messages.
**Acceptance**:
- No empty catch blocks (except with explicit comment justification).
**Labels**: tech-debt, reliability

## 8. Memory Management for MagickImage in UndoRedo
**Problem**: `MagickImage` instances created but never disposed.
**Actions**:
- Use `using` or manual `Dispose()`.
- Optionally cache `BitmapSource` instead of reloading file path.
**Acceptance**:
- No disposable leaks flagged by static analysis.
**Labels**: bug, performance

## 9. Redesign UndoRedo to Generic Change Model
**Problem**: Undo stack tightly coupled to Image & Grid specifics.
**Actions**:
- Introduce `IUndoableChange { void Apply(); void Revert(); }`.
- Implement concrete changes (ImageChange, ResizeChange, etc.).
- Simplify `UndoRedo` to stack of interface.
**Acceptance**:
- Existing functionality preserved; easier extension.
**Labels**: refactor, architecture

## 10. Async Autosave & UI Thread Safety
**Problem**: Potential threading & blocking issues; collection updates not marshaled.
**Actions**:
- Make `AutosaveProject` async.
- Marshal collection updates via Dispatcher.
- Add in-flight guard (bool flag) or debounce timer.
**Acceptance**:
- Autosave does not freeze UI; no cross-thread exceptions.
**Labels**: performance, refactor

## 11. Consolidate Magic Constants
**Problem**: Hard-coded values (thumbnail width, tolerance, max projects) scattered.
**Actions**:
- Create `Defaults` static class or options record.
- Replace literals with named constants.
**Acceptance**:
- Single source for configurable numeric constants.
**Labels**: refactor, maintainability

## 12. Geometry & Formatting Helpers for Measurements
**Problem**: Polygon control contains raw geometric formulas & format logic.
**Actions**:
- Add `GeometryMathHelper` (Perimeter, Area).
- Add `MeasurementFormattingHelper` (FormatPerimeter, FormatPerimeterArea).
- Replace inline code.
**Acceptance**:
- Polygon control delegates calculations/formatting.
**Labels**: refactor, cleanup

## 13. Create BaseMeasurementControl
**Problem**: Repeated context menu / copy / remove logic across measurement controls.
**Actions**:
- Abstract base class handling shared events & context menu.
- Migrate one control first (pilot), then others.
**Acceptance**:
- Duplicate code eliminated; derived classes minimal.
**Labels**: refactor, DRY

## 14. Update PolygonMeasurementControl Display Logic
**Problem**: Multiple separate calls for update & position text; verbose debug.
**Actions**:
- Merge into `UpdateDisplay()` method.
- Guard debug output with `#if DEBUG`.
- Remove redundant state logs.
**Acceptance**:
- Single entry point updates text + layout.
**Labels**: cleanup

## 15. Convert Pure DTOs to Records
**Problem**: DTOs are mutable boilerplate classes.
**Actions**:
- Identify DTOs with no mutable lifecycle requirements.
- Convert to `record` or `record struct`.
**Acceptance**:
- Cleaner code, improved value semantics.
**Labels**: refactor, modernization

## 16. Naming Consistency Policy
**Problem**: Inconsistent naming conventions (e.g., *Async misuse).
**Actions**:
- Document naming guidelines in CONTRIBUTING.md.
- Apply to existing methods.
**Acceptance**:
- Project free of naming inconsistencies flagged.
**Labels**: documentation, refactor

## 17. Add Basic Unit Tests (Infrastructure)
**Problem**: Hard to verify refactors safely.
**Actions**:
- Add test project.
- Cover: RecentProjects serialization round-trip, geometry math, UndoRedo basic flow.
**Acceptance**:
- CI green with baseline tests.
**Labels**: testing, enhancement

## 18. Introduce Optional Logging Abstraction
**Problem**: No consistent logging; helpful for future diagnostics.
**Actions**:
- Wrap Debug / Trace or integrate `Microsoft.Extensions.Logging`.
- Replace ad-hoc `Debug.WriteLine`.
**Acceptance**:
- Central logger used; can be silenced or redirected.
**Labels**: enhancement, maintainability

## 19. Backward Compatibility Review for .mcm Format
**Problem**: Upcoming changes might alter serialization.
**Actions**:
- Document current schema.
- Add version marker if not present.
- Plan migration strategy (if necessary).
**Acceptance**:
- Clear backward compatibility policy documented.
**Labels**: documentation, enhancement

## 20. Introduce Result Types for Persistence
**Problem**: Boolean returns hide error reasons.
**Actions**:
- Implement `record PersistenceResult(bool Success, string? Error);`.
- Replace bool returns in persistence operations.
**Acceptance**:
- Callers can surface meaningful error messages.
**Labels**: refactor, usability

## 21. Provide Cancellation Support for Long Operations
**Problem**: Large images / saves may block.
**Actions**:
- Add `CancellationToken` params to async persistence & image operations.
- Honor cancellation in loops / I/O.
**Acceptance**:
- Operations can be cancelled without leaks / partial corruption.
**Labels**: enhancement, performance

## 22. Debounce / Throttle Autosave
**Problem**: Rapid edits may cause excessive writes.
**Actions**:
- Introduce debounce timer or coalescing mechanism.
- Configurable interval (e.g., 2–5 seconds idle).
**Acceptance**:
- Fewer redundant writes; no data loss.
**Labels**: enhancement, performance

## 23. Improve Recent Project Cleanup Strategy
**Problem**: Old thumbnails / orphan files may accumulate.
**Actions**:
- Add cleanup routine: remove thumbnails without matching package & vice versa.
- Run at startup or scheduled.
**Acceptance**:
- No orphaned recent project artifacts after cleanup.
**Labels**: enhancement, maintenance

## 24. UI Feedback for Autosave Failures
**Problem**: Silent failures not visible to user.
**Actions**:
- Surface non-intrusive toast / status bar message on failure.
- Log details internally.
**Acceptance**:
- User informed if autosave fails.
**Labels**: usability, enhancement

## 25. Document Developer Setup & Architecture
**Problem**: New contributors need guidance.
**Actions**:
- Add ARCHITECTURE.md summarizing layers & services.
- Expand README with build steps, tests, coding standards.
**Acceptance**:
- Clear onboarding docs available.
**Labels**: documentation

---
## Suggested Milestone Grouping
- Milestone 1 (Stabilize Core): Issues 1–4, 8, 11
- Milestone 2 (Services & Async): 5, 6, 7, 10, 20
- Milestone 3 (Measurement Refactor): 12, 13, 14, 15
- Milestone 4 (Undo/Redo & Reliability): 9, 18, 21, 22
- Milestone 5 (UX & Documentation): 16, 17, 19, 23, 24, 25

---
## Open Questions (Track in Discussion or Single Issue)
1. Adopt dependency injection via `Microsoft.Extensions.DependencyInjection`? 
2. Backward compatibility guarantee for `.mcm`? Add schema version now?
3. Scope of Undo/Redo (extend to measurements & metadata?)
4. Accept adding test project dependencies (e.g., xUnit + FluentAssertions)?
5. Introduce CommunityToolkit.Mvvm to reduce boilerplate?

---
## Implementation Order Recommendation
1. Low-risk infrastructural abstractions (Issues 2–6, 11).
2. Safety fixes (Issue 8 memory leak).
3. Async + autosave modernization (Issue 10, 22, 20).
4. Measurement UI refactors (12–15).
5. Undo/Redo overhaul (9) after stability.
6. Logging & observability (7, 18).
7. Tests & documentation (16, 17, 19, 25).
8. Polishing tasks (21, 23, 24).

---
Generated plan can now be split into individual GitHub issues.
