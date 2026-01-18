# MagickCrop Testing Patterns and Best Practices

This document outlines the testing patterns, conventions, and best practices used throughout the MagickCrop test suite.

## Table of Contents

1. [Test Organization](#test-organization)
2. [Unit vs Integration Tests](#unit-vs-integration-tests)
3. [ViewModel Testing](#viewmodel-testing)
4. [Service Testing](#service-testing)
5. [Async Testing](#async-testing)
6. [Mocking Strategies](#mocking-strategies)
7. [Assertion Patterns](#assertion-patterns)
8. [Test Naming Conventions](#test-naming-conventions)
9. [Running Tests](#running-tests)

---

## Test Organization

### Directory Structure

```
MagickCrop.Tests/
├── Base/                          # Test infrastructure and base classes
│   ├── IntegrationTestBase.cs     # Integration test foundation
│   ├── TestServiceInterfaces.cs   # Mock service contracts
│   └── ViewModelTestBase.cs       # ViewModel testing base
├── Fixtures/                      # Test data and fixtures
│   └── TestServiceFixture.cs      # DI container configuration
├── Mocks/                         # Mock implementations
│   ├── MockFileDialogService.cs
│   ├── MockClipboardService.cs
│   ├── MockImageProcessingService.cs
│   └── MockNavigationService.cs
├── ViewModels/                    # ViewModel unit tests
│   ├── MainWindowViewModelTests.cs
│   ├── AboutWindowViewModelTests.cs
│   └── MeasurementViewModelTests.cs
├── Services/                      # Service unit tests
│   ├── RecentProjectsServiceTests.cs
│   ├── ImageProcessingServiceTests.cs
│   └── ClipboardServiceTests.cs
├── Integration/                   # Integration tests (workflows)
│   ├── FileOperationWorkflowTests.cs
│   ├── MeasurementWorkflowTests.cs
│   └── ProjectSaveLoadTests.cs
└── GlobalUsings.cs               # Global using statements
```

### Test Project Configuration

The test project is configured with:
- **Framework**: MSTest v3
- **Mocking**: Moq v4
- **Coverage**: Coverlet with OpenCover format
- **DI Container**: Microsoft.Extensions.DependencyInjection

---

## Unit vs Integration Tests

### Unit Tests

**Purpose**: Test a single component in isolation with all dependencies mocked.

**Characteristics**:
- Fast execution (< 100ms typically)
- No external dependencies
- All collaborators are mocked
- Focus on a single responsibility
- Located in `ViewModels/` or `Services/` folders

**Example**:
```csharp
[TestClass]
public class DistanceMeasurementViewModelTests
{
    private Mock<IImageProcessingService> _mockImageService;
    private DistanceMeasurementViewModel _viewModel;

    [TestInitialize]
    public void Setup()
    {
        _mockImageService = new Mock<IImageProcessingService>();
        _viewModel = new DistanceMeasurementViewModel(_mockImageService.Object);
    }

    [TestMethod]
    public void CalculateDistance_WithValidPoints_ReturnsCorrectValue()
    {
        // Arrange
        var point1 = new Point(0, 0);
        var point2 = new Point(3, 4);

        // Act
        var distance = _viewModel.CalculateDistance(point1, point2);

        // Assert
        Assert.AreEqual(5.0, distance);
    }
}
```

### Integration Tests

**Purpose**: Test complete workflows with realistic service interactions.

**Characteristics**:
- Slower execution (100ms - 1s)
- Real services for business logic, mocked I/O
- Test complete user workflows
- Verify service interactions
- Located in `Integration/` folder

**Example**:
```csharp
[TestClass]
public class FileOperationWorkflowTests : IntegrationTestBase
{
    [TestMethod]
    public async Task LoadProject_SaveProject_PreservesData()
    {
        // Arrange
        var testProject = CreateTestProject("TestWorkflow");
        var projectPath = Path.Combine(Path.GetTempPath(), "test.mcm");

        // Act - Save
        await RecentProjectsService.SaveProjectAsync(testProject, projectPath);
        
        // Act - Load
        var loadedProject = await RecentProjectsService.LoadProjectAsync(projectPath);

        // Assert
        Assert.IsNotNull(loadedProject);
        Assert.AreEqual(testProject.Metadata.FileName, loadedProject.Metadata.FileName);
    }
}
```

---

## ViewModel Testing

### Setup Pattern

Always inherit from `ViewModelTestBase` for consistent setup:

```csharp
[TestClass]
public class MainWindowViewModelTests : ViewModelTestBase
{
    private Mock<IRecentProjectsService> _mockRecentProjects;
    private MainWindowViewModel _viewModel;

    [TestInitialize]
    public override void Setup()
    {
        base.Setup(); // Sets up common mocks

        _mockRecentProjects = new Mock<IRecentProjectsService>();
        _viewModel = new MainWindowViewModel(
            _mockRecentProjects.Object,
            MockImageService.Object,
            MockFileDialogService.Object
        );
    }
}
```

### Command Testing

Test commands using Moq for side effects:

```csharp
[TestMethod]
public void OpenFileCommand_WhenExecuted_CallsFileDialog()
{
    // Arrange
    MockFileDialogService.Setup(x => x.ShowOpenFileDialogAsync())
        .ReturnsAsync("test.jpg");

    // Act
    _viewModel.OpenFileCommand.Execute(null);

    // Assert
    MockFileDialogService.Verify(x => x.ShowOpenFileDialogAsync(), Times.Once);
}
```

### Property Change Notification Testing

Verify INotifyPropertyChanged implementations:

```csharp
[TestMethod]
public void SelectedMeasurement_WhenChanged_RaisesPropertyChanged()
{
    // Arrange
    var propertyChangedFired = false;
    _viewModel.PropertyChanged += (s, e) =>
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedMeasurement))
            propertyChangedFired = true;
    };

    // Act
    _viewModel.SelectedMeasurement = new DistanceMeasurement();

    // Assert
    Assert.IsTrue(propertyChangedFired);
}
```

---

## Service Testing

### RecentProjectsService Testing

```csharp
[TestMethod]
public async Task SaveProject_CreatesValidMCMFile()
{
    // Arrange
    var project = new MagickCropMeasurementPackage { /* ... */ };
    var tempPath = Path.GetTempFileName();

    try
    {
        // Act
        await _service.SaveProjectAsync(project, tempPath);

        // Assert
        Assert.IsTrue(File.Exists(tempPath));
        using (var archive = new ZipArchive(File.OpenRead(tempPath)))
        {
            Assert.IsTrue(archive.Entries.Any(e => e.Name == "metadata.json"));
            Assert.IsTrue(archive.Entries.Any(e => e.Name.EndsWith(".jpg")));
        }
    }
    finally
    {
        File.Delete(tempPath);
    }
}
```

### ImageProcessingService Testing

```csharp
[TestMethod]
public void ResizeImage_WithValidDimensions_ReturnsBitmap()
{
    // Arrange
    var sourceImage = CreateTestImageBytes(800, 600);

    // Act
    var resized = _service.ResizeImage(sourceImage, 400, 300);

    // Assert
    Assert.IsNotNull(resized);
    // Verify dimensions through image analysis
}
```

---

## Async Testing

### Task-Based Async Tests

For async methods, use `async Task` test methods:

```csharp
[TestMethod]
public async Task LoadProjectAsync_WithValidPath_ReturnsProject()
{
    // Arrange
    var projectPath = "valid_project.mcm";
    MockFileDialogService.Setup(x => x.GetProjectAsync(projectPath))
        .ReturnsAsync(new MagickCropMeasurementPackage());

    // Act
    var result = await _viewModel.LoadProjectAsync(projectPath);

    // Assert
    Assert.IsNotNull(result);
}
```

### Timeout Testing

Use `AssertCompletesWithinAsync` for timeout validation:

```csharp
[TestMethod]
public async Task ProcessLargeImage_CompletesWithinTimeout()
{
    // Arrange & Act & Assert
    await IntegrationTestBase.AssertCompletesWithinAsync(
        () => _viewModel.ProcessImageAsync(_largeImageBytes),
        timeoutMs: 5000
    );
}
```

### Exception Testing

```csharp
[TestMethod]
[ExpectedException(typeof(ArgumentException))]
public async Task LoadProject_WithNullPath_ThrowsException()
{
    await _viewModel.LoadProjectAsync(null!);
}

// Or using Assert.ThrowsExceptionAsync
[TestMethod]
public async Task LoadProject_WithInvalidPath_ThrowsIOException()
{
    await Assert.ThrowsExceptionAsync<IOException>(
        () => _viewModel.LoadProjectAsync("invalid_path")
    );
}
```

---

## Mocking Strategies

### Mock vs Fake

- **Mock**: Verify that methods were called with correct parameters
- **Fake**: Provide working implementation for test purposes

### When to Use Each

**Use Mocks for**:
- UI services (FileDialog, Clipboard, Navigation)
- External dependencies
- Verifying method calls and interactions

**Use Fakes for**:
- Business logic services (RecentProjects, ImageProcessing)
- When you need real behavior but in a controlled manner

### Mock Verification

```csharp
// Verify was called exactly once
MockFileDialogService.Verify(
    x => x.ShowOpenFileDialogAsync(),
    Times.Once
);

// Verify was called with specific parameters
MockImageService.Verify(
    x => x.ResizeImage(It.IsAny<byte[]>(), 400, 300),
    Times.Once
);

// Verify was never called
MockNavigationService.Verify(
    x => x.ShowWindowAsync<AboutWindowViewModel>(),
    Times.Never
);
```

---

## Assertion Patterns

### MSTest Assertions

```csharp
// Equality
Assert.AreEqual(expected, actual);
Assert.AreNotEqual(notExpected, actual);

// Null checks
Assert.IsNull(actual);
Assert.IsNotNull(actual);

// Boolean
Assert.IsTrue(condition);
Assert.IsFalse(condition);

// Collections
CollectionAssert.AreEqual(expected, actual);
CollectionAssert.Contains(collection, element);
CollectionAssert.IsEmpty(collection);

// Exception testing
Assert.ThrowsException<ArgumentException>(() => action());
```

### Custom Assertions

For common patterns, create helpers:

```csharp
private void AssertMeasurementValid(DistanceMeasurement measurement)
{
    Assert.IsNotNull(measurement);
    Assert.IsTrue(measurement.Distance >= 0);
    Assert.IsTrue(measurement.Unit != MeasurementUnit.None);
}

private void AssertProjectLoadsCorrectly(MagickCropMeasurementPackage package)
{
    Assert.IsNotNull(package);
    Assert.IsNotNull(package.Metadata);
    Assert.IsTrue(package.Metadata.ImageWidth > 0);
    Assert.IsTrue(package.Metadata.ImageHeight > 0);
    AssertMeasurementValid(package.Measurements);
}
```

---

## Test Naming Conventions

### Format: `MethodName_Scenario_ExpectedResult`

```csharp
[TestMethod]
public void CalculateDistance_WithValidPoints_ReturnsCorrectValue() { }

[TestMethod]
public void SaveProject_WithNullPath_ThrowsArgumentNullException() { }

[TestMethod]
public async Task LoadProjectAsync_WithCorruptedFile_ThrowsIOException() { }

[TestMethod]
public void OpenFileCommand_WhenExecuted_CallsFileDialog() { }
```

### Guidelines

- Use clear, descriptive names
- Start with the method being tested
- Include the scenario/condition
- End with expected result
- Avoid abbreviations
- Use present tense ("Returns", "Throws", "Calls")

---

## Running Tests

### Run All Tests

```bash
dotnet test
```

### Run with Coverage Report

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Run Specific Test Class

```bash
dotnet test --filter "ClassName=DistanceMeasurementViewModelTests"
```

### Run Specific Test Method

```bash
dotnet test --filter "Name=CalculateDistance_WithValidPoints_ReturnsCorrectValue"
```

### Run Tests in Visual Studio Test Explorer

1. Open Test Explorer (Test → Test Explorer)
2. Search for tests by name or class
3. Right-click and select "Run Selected Tests"
4. View results in Test Explorer window

### Debugging Tests

1. Set breakpoint in test or tested code
2. Right-click test in Test Explorer
3. Select "Debug Selected Tests"
4. Use Visual Studio debugger as normal

---

## Code Coverage

### Coverage Goals

- **ViewModels**: 80%+ line coverage
- **Services**: 70%+ line coverage
- **Utilities**: 60%+ line coverage

### Viewing Coverage

Coverage reports are generated in OpenCover format. Use tools like:
- ReportGenerator (generates HTML reports)
- Visual Studio Coverage tools
- Azure DevOps coverage widgets

### Excluding from Coverage

Mark code to exclude using:

```csharp
#pragma warning disable CS0162 // Unreachable code
// Code excluded from coverage
#pragma warning restore CS0162
```

---

## Common Test Scenarios

### Testing Collection Changes

```csharp
[TestMethod]
public void AddMeasurement_AddsToCollection()
{
    // Arrange
    var measurements = new ObservableCollection<Measurement>();
    measurements.CollectionChanged += (s, e) => { /* verify */ };

    // Act
    measurements.Add(new DistanceMeasurement());

    // Assert
    Assert.AreEqual(1, measurements.Count);
}
```

### Testing Data Persistence

```csharp
[TestMethod]
public async Task SaveAndLoadProject_PreservesMeasurements()
{
    // Arrange
    var tempPath = Path.GetTempFileName();
    var originalProject = CreateTestProject();

    try
    {
        // Act
        await _service.SaveProjectAsync(originalProject, tempPath);
        var loadedProject = await _service.LoadProjectAsync(tempPath);

        // Assert
        Assert.AreEqual(originalProject.Measurements.Count, loadedProject.Measurements.Count);
    }
    finally
    {
        File.Delete(tempPath);
    }
}
```

### Testing Error Scenarios

```csharp
[TestMethod]
public async Task LoadProject_WithMissingFile_HandlesGracefully()
{
    // Arrange
    var nonexistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    // Act
    var result = await _viewModel.TryLoadProjectAsync(nonexistentPath);

    // Assert
    Assert.IsFalse(result);
    Assert.IsNotNull(_viewModel.LastError);
}
```

---

## Real Codebase Examples

### MainWindowViewModelTests.cs
Located in `MagickCrop.Tests/ViewModels/MainWindowViewModelTests.cs`:
- Tests initialization and state management
- Tests command execution
- Tests property binding updates

### RecentProjectsServiceTests.cs
Located in `MagickCrop.Tests/Services/RecentProjectsServiceTests.cs`:
- Tests file I/O operations
- Tests project serialization
- Tests collection management

### FileOperationWorkflowTests.cs
Located in `MagickCrop.Tests/Integration/FileOperationWorkflowTests.cs`:
- Tests complete file save/load cycles
- Tests project metadata preservation
- Tests error recovery

---

## Best Practices Summary

1. **Arrange-Act-Assert**: Structure every test with these three phases
2. **One assertion focus**: Each test should focus on one logical assertion
3. **Descriptive names**: Use clear, self-documenting test names
4. **Isolated tests**: Tests should not depend on each other
5. **Mock external**: Mock UI, file, and network dependencies
6. **Real business logic**: Keep business logic real for integrity
7. **Setup/Teardown**: Use [TestInitialize]/[TestCleanup] properly
8. **No test interdependence**: Tests can run in any order
9. **Meaningful errors**: Make assertion failure messages clear
10. **Refactor with tests**: Refactor tested code confidently

---

*Last Updated: January 2026*
*Framework: MSTest v3, Moq v4, Coverlet v6*
