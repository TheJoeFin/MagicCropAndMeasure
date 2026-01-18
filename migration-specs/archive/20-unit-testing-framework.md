# Step 20: Unit Testing Framework Setup

## Objective

Establish a comprehensive unit testing framework for MagickCrop now that MVVM architecture is in place. Create test infrastructure and write initial tests for core ViewModels and Services to ensure business logic reliability.

## Prerequisites

- All previous steps (01-19) completed
- MVVM architecture fully implemented with DI container
- Application builds successfully with 0 errors

---

## ⚠️ This step has been broken into smaller sub-steps for easier implementation

**Complete these sub-steps in order:**

| Sub-Step | Description | Estimated Effort |
|----------|-------------|-----------------|
| **20a** | Create test project and configure MSTest framework | 30 min |
| **20b** | Create mock/fake implementations of all services | 45 min |
| **20c** | Create test base classes for ViewModel testing | 30 min |
| **20d** | Write tests for MainWindowViewModel initialization | 45 min |
| **20e** | Write tests for ImageProcessingService | 45 min |
| **20f** | Write tests for RecentProjectsService | 45 min |
| **20g** | Write tests for measurement ViewModels | 60 min |
| **20h** | Set up code coverage reporting | 30 min |
| **20i** | Create integration test base infrastructure | 45 min |
| **20j** | Document testing patterns and best practices | 30 min |

Each sub-step should be its own commit with passing tests and a working build.

---

## Step 20a: Create Test Project and Configure MSTest Framework

### Task: Create MagickCrop.Tests Project

**File: MagickCrop.Tests/MagickCrop.Tests.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.20348.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
    <LangVersion>13</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.2.2" />
    <PackageReference Include="MSTest.TestFramework" Version="3.2.2" />
    <PackageReference Include="Moq" Version="4.20.70" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MagickCrop\MagickCrop.csproj" />
  </ItemGroup>

</Project>
```

### Create Test Structure

**Folder: MagickCrop.Tests/**
```
MagickCrop.Tests/
├── Fixtures/
│   └── TestServiceFixture.cs
├── Mocks/
│   ├── MockFileDialogService.cs
│   ├── MockClipboardService.cs
│   ├── MockImageProcessingService.cs
│   └── MockNavigationService.cs
├── ViewModels/
│   ├── MainWindowViewModelTests.cs
│   ├── AboutWindowViewModelTests.cs
│   └── MeasurementViewModelTests.cs
├── Services/
│   ├── RecentProjectsServiceTests.cs
│   └── ImageProcessingServiceTests.cs
└── GlobalUsings.cs
```

**File: MagickCrop.Tests/GlobalUsings.cs**

```csharp
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Moq;
global using MagickCrop.Services.Interfaces;
global using MagickCrop.ViewModels;
global using System.Windows.Media.Imaging;
```

### Validation Checklist for 20a

- [ ] Test project created in MagickCrop.Tests folder
- [ ] MSTest, Moq packages installed
- [ ] Project structure matches above
- [ ] Solution builds: `dotnet build -c Debug`
- [ ] Test project compiles without errors
- [ ] Can run tests: `dotnet test`

---

## Step 20h: Set Up Code Coverage Reporting

### Task: Add Coverlet NuGet Package and Configure Coverage

**File: MagickCrop.Tests/MagickCrop.Tests.csproj**

Update the `<ItemGroup>` with PackageReferences to include:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
  <PackageReference Include="MSTest.TestAdapter" Version="3.2.2" />
  <PackageReference Include="MSTest.TestFramework" Version="3.2.2" />
  <PackagePackageReference Include="coverlet.collector" Version="6.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Moq" Version="4.20.70" />
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
</ItemGroup>
```

### Configure Coverlet for CI/CD

**Create/Update: MagickCrop.Tests/.runsettings**

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>opencover</Format>
          <Exclude>[MagickCrop.Tests]*</Exclude>
          <IncludeTestAssembly>false</IncludeTestAssembly>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### Code Coverage Commands

Run tests with coverage reporting:

```bash
# Generate coverage in OpenCover format (CI/CD compatible)
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=./coverage.opencover.xml

# Generate coverage in multiple formats
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=json,lcov,opencover

# View coverage in console output
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=json --logger "console;verbosity=detailed"
```

### Coverage Goals

Target the following coverage thresholds:

| Component | Target Coverage | Priority |
|-----------|-----------------|----------|
| ViewModels | 75%+ | High |
| Services | 70%+ | High |
| Helpers/Utilities | 60%+ | Medium |
| Models/DTOs | 50%+ | Low |

### Documentation: Coverage Configuration

**Create: MagickCrop.Tests/COVERAGE.md**

```markdown
# Code Coverage Setup

## Running Coverage Analysis

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Interpreting Results

- **OpenCover XML**: Use with SonarQube or other CI/CD tools
- **Coverage reports**: Located in `bin/Debug/coverage.opencover.xml`

## Minimum Coverage by Layer

- **Presentation ViewModels**: 75%
- **Business Logic (Services)**: 70%
- **Utilities/Helpers**: 60%

## Excluding from Coverage

Test files and generated code are automatically excluded. To exclude specific classes:

```csharp
[ExcludeFromCodeCoverage]
public class ClassToExclude { }
```
```

### Validation Checklist for 20h

- [ ] Coverlet.collector NuGet package installed
- [ ] .runsettings file created and configured
- [ ] Coverage commands execute without errors: `dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover`
- [ ] OpenCover XML file generated: `coverage.opencover.xml`
- [ ] COVERAGE.md documentation created
- [ ] All existing tests still pass
- [ ] Solution builds: `dotnet build -c Debug`

---

## Step 20i: Create Integration Test Base Infrastructure

### Task: Create IntegrationTestBase Class

**File: MagickCrop.Tests/Base/IntegrationTestBase.cs**

```csharp
/// <summary>
/// Base class for integration tests that require full application setup with real services.
/// Provides helper methods for creating test containers, projects, and mock services.
/// </summary>
[TestClass]
public abstract class IntegrationTestBase
{
    protected IServiceProvider TestContainer { get; private set; } = null!;
    protected Mock<ITestOutputService> MockTestOutputService { get; private set; } = null!;
    protected Mock<ITestFileService> MockTestFileService { get; private set; } = null!;

    [TestInitialize]
    public virtual void Initialize()
    {
        CreateTestContainer();
    }

    [TestCleanup]
    public virtual void Cleanup()
    {
        (TestContainer as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Creates and configures the test DI container with real services where appropriate.
    /// </summary>
    protected void CreateTestContainer()
    {
        var services = new ServiceCollection();

        // Register real application services
        // RegisterApplicationServices(services);

        // Register mock services for UI and file I/O
        MockTestOutputService = new Mock<ITestOutputService>();
        MockTestFileService = new Mock<ITestFileService>();

        services.AddSingleton(MockTestOutputService.Object);
        services.AddSingleton(MockTestFileService.Object);

        TestContainer = services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a test project with sample measurements and metadata.
    /// </summary>
    protected MagickCropMeasurementPackage CreateTestProject(
        string projectName = "TestProject",
        int width = 800,
        int height = 600)
    {
        var metadata = new PackageMetadata
        {
            ProjectName = projectName,
            CreatedAt = DateTime.Now,
            ImageWidth = width,
            ImageHeight = height,
            MeasurementUnits = "pixels"
        };

        var package = new MagickCropMeasurementPackage
        {
            Metadata = metadata,
            Measurements = new MeasurementCollection()
        };

        return package;
    }

    /// <summary>
    /// Loads a test image from the test data directory.
    /// </summary>
    protected BitmapSource LoadTestImage(string imageName = "test-image.png")
    {
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", imageName);

        if (!File.Exists(testDataPath))
        {
            throw new FileNotFoundException($"Test image not found: {testDataPath}");
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(testDataPath);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }

    /// <summary>
    /// Verifies that a service was called with expected parameters.
    /// </summary>
    protected void VerifyServiceCall<T>(
        Mock<T> mock,
        string methodName,
        Times times) where T : class
    {
        // Implementation for verifying mock calls in integration tests
    }
}
```

### Task: Create Test Service Interfaces

**File: MagickCrop.Tests/Base/ITestOutputService.cs**

```csharp
/// <summary>
/// Mock service for capturing test output and UI messages.
/// </summary>
public interface ITestOutputService
{
    void LogMessage(string message);
    void LogError(string error);
    void LogWarning(string warning);
    IReadOnlyList<string> GetMessages();
    void Clear();
}
```

**File: MagickCrop.Tests/Base/ITestFileService.cs**

```csharp
/// <summary>
/// Mock service for file operations in tests.
/// </summary>
public interface ITestFileService
{
    Task<bool> FileExistsAsync(string filePath);
    Task<byte[]> ReadFileAsync(string filePath);
    Task WriteFileAsync(string filePath, byte[] data);
    Task DeleteFileAsync(string filePath);
    void SetupFileSystemMock(Dictionary<string, byte[]> files);
}
```

### Test Directory Structure

**Create folder: MagickCrop.Tests/TestData/**

```
MagickCrop.Tests/TestData/
├── test-image.png
├── sample-project.mcm
└── corrupted-project.mcm
```

**Add test images**: Place small sample images for testing image processing operations.

### Validation Checklist for 20i

- [ ] IntegrationTestBase.cs created in MagickCrop.Tests/Base/
- [ ] ITestOutputService interface created and implemented
- [ ] ITestFileService interface created and implemented
- [ ] CreateTestContainer() method works without errors
- [ ] CreateTestProject() returns valid package with metadata
- [ ] LoadTestImage() loads test images from TestData directory
- [ ] TestData directory created with sample test images
- [ ] Can inherit from IntegrationTestBase in test classes
- [ ] All existing tests still pass

---

## Step 20j: Document Testing Patterns and Best Practices

### Task: Create Testing Patterns Documentation

**Create: MagickCrop.Tests/TESTING_PATTERNS.md**

```markdown
# Testing Patterns and Best Practices

## Overview

This document outlines the testing patterns and best practices used in MagickCrop test suite.

## Test Categories

### Unit Tests

Unit tests verify individual components in isolation using mocks for dependencies.

**When to use:**
- Testing service methods with well-defined inputs/outputs
- Testing ViewModel command logic
- Testing calculation and utility methods
- Testing edge cases and error scenarios

**Example:**
```csharp
[TestClass]
public class MeasurementCalculatorTests : ServiceTestBase
{
    [TestMethod]
    public void CalculateDistance_WithValidPoints_ReturnsCorrectDistance()
    {
        // Arrange
        var calculator = new MeasurementCalculator();
        var point1 = new Point(0, 0);
        var point2 = new Point(3, 4);

        // Act
        var distance = calculator.CalculateDistance(point1, point2);

        // Assert
        Assert.AreEqual(5.0, distance);
    }
}
```

### Integration Tests

Integration tests verify multiple components working together with minimal mocking.

**When to use:**
- Testing complete workflows (load → measure → save)
- Testing service interactions
- Testing state management across ViewModels
- Testing file I/O operations with real storage

**Example:**
```csharp
[TestClass]
public class ProjectLoadingTests : IntegrationTestBase
{
    [TestMethod]
    public async Task LoadProject_WithValidFile_LoadsAllMeasurements()
    {
        // Arrange
        var projectPath = "test-project.mcm";
        var project = CreateTestProject("LoadTest");

        // Act
        var loaded = await LoadProjectAsync(projectPath);

        // Assert
        Assert.IsNotNull(loaded);
        Assert.AreEqual(project.Metadata.ProjectName, loaded.Metadata.ProjectName);
    }
}
```

## Mocking Strategies

### Mock Services with Moq

Use Moq to create mock implementations of interfaces for dependency injection:

```csharp
// Create a mock with specific setup
var mockClipboard = new Mock<IClipboardService>();
mockClipboard
    .Setup(x => x.HasImage())
    .Returns(true);

// Verify method was called
mockClipboard.Verify(x => x.CopyImage(It.IsAny<BitmapSource>()), Times.Once);

// Set up with callback
mockClipboard
    .Setup(x => x.CopyImage(It.IsAny<BitmapSource>()))
    .Callback<BitmapSource>(image => Console.WriteLine("Image copied"));
```

### Fake Implementations

For complex behaviors, create fake implementations instead of mocks:

```csharp
public class FakeRecentProjectsService : IRecentProjectsService
{
    private readonly List<RecentProjectInfo> _projects = [];

    public async Task AddRecentProjectAsync(RecentProjectInfo project)
    {
        _projects.Add(project);
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<RecentProjectInfo>> GetRecentProjectsAsync()
    {
        return await Task.FromResult(_projects.AsReadOnly());
    }
}
```

## Testing ViewModels

### ViewModel Testing Pattern

```csharp
[TestClass]
public class MeasurementViewModelTests : ViewModelTestBase
{
    private MeasurementViewModel _viewModel = null!;
    private Mock<IMeasurementService> _mockMeasurementService = null!;

    [TestInitialize]
    public override void Initialize()
    {
        base.Initialize();
        _mockMeasurementService = new Mock<IMeasurementService>();
        _viewModel = new MeasurementViewModel(_mockMeasurementService.Object);
    }

    [TestMethod]
    public void AddMeasurement_WithValidData_UpdatesCollection()
    {
        // Arrange
        var measurement = new DistanceMeasurement { Distance = 42.5 };
        var originalCount = _viewModel.Measurements.Count;

        // Act
        _viewModel.AddMeasurementCommand.Execute(measurement);

        // Assert
        Assert.AreEqual(originalCount + 1, _viewModel.Measurements.Count);
        Assert.IsTrue(_viewModel.Measurements.Contains(measurement));
    }

    [TestMethod]
    public async Task LoadMeasurements_WithServiceData_PopulatesCollection()
    {
        // Arrange
        var measurements = new[]
        {
            new DistanceMeasurement { Distance = 10 },
            new DistanceMeasurement { Distance = 20 }
        };

        _mockMeasurementService
            .Setup(x => x.GetMeasurementsAsync(It.IsAny<string>()))
            .ReturnsAsync(measurements);

        // Act
        await _viewModel.LoadMeasurementsAsync("project-id");

        // Assert
        Assert.AreEqual(2, _viewModel.Measurements.Count);
        _mockMeasurementService.Verify(x => x.GetMeasurementsAsync("project-id"), Times.Once);
    }
}
```

## Testing Services

### Service Testing Pattern

```csharp
[TestClass]
public class ImageProcessingServiceTests : ServiceTestBase
{
    private ImageProcessingService _service = null!;

    [TestInitialize]
    public override void Initialize()
    {
        base.Initialize();
        _service = new ImageProcessingService();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task ResizeImage_WithNullImage_ThrowsArgumentNullException()
    {
        // Act
        await _service.ResizeImageAsync(null!, 800, 600);
    }

    [TestMethod]
    public async Task ResizeImage_WithValidImage_ReturnsResizedImage()
    {
        // Arrange
        var originalImage = CreateTestBitmap(1600, 1200);

        // Act
        var resized = await _service.ResizeImageAsync(originalImage, 800, 600);

        // Assert
        Assert.IsNotNull(resized);
        Assert.AreEqual(800, resized.PixelWidth);
        Assert.AreEqual(600, resized.PixelHeight);
    }
}
```

## Async Testing Patterns

### Testing Async Methods

```csharp
[TestMethod]
public async Task LoadProjectAsync_WithValidPath_ReturnsProject()
{
    // Arrange
    var projectPath = "valid-project.mcm";

    // Act
    var project = await LoadProjectAsync(projectPath);

    // Assert
    Assert.IsNotNull(project);
}

// For testing timeouts and cancellation
[TestMethod]
[ExpectedException(typeof(OperationCanceledException))]
public async Task LongRunningOperation_WithCancellation_ThrowsOperationCanceledException()
{
    // Arrange
    var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

    // Act
    await _service.LongRunningOperationAsync(cts.Token);
}
```

## Assertion Best Practices

### Clear and Specific Assertions

```csharp
// ✓ Good: Specific and descriptive
Assert.AreEqual(42, result, "Calculation should return 42 for valid input");
Assert.IsTrue(collection.Contains(item), "Collection should contain the added item");

// ✗ Avoid: Vague assertions
Assert.IsTrue(result > 0);
Assert.IsFalse(String.IsNullOrEmpty(name));
```

### Multiple Assertions in Single Test

When multiple assertions validate a single logical outcome:

```csharp
[TestMethod]
public void CreateMeasurement_WithValidData_PropertiesAreSet()
{
    // Arrange & Act
    var measurement = CreateMeasurement(10, 20, 30);

    // Assert - all related to the same outcome
    Assert.AreEqual(10, measurement.StartX);
    Assert.AreEqual(20, measurement.StartY);
    Assert.AreEqual(30, measurement.Distance);
}
```

## Test Naming Conventions

Follow this naming pattern for test methods:

```
[MethodName]_[Condition]_[ExpectedResult]
```

**Examples:**
```csharp
public void CalculateDistance_WithNegativeValues_ReturnsAbsoluteValue()
public void SaveProject_WithoutPermission_ThrowsUnauthorizedAccessException()
public void LoadImage_WithCorruptedFile_ShowsErrorMessage()
public void AddMeasurement_WithDuplicateId_ReplacesExisting()
```

## Test Data Builders

Use Test Data Builders for complex object creation:

```csharp
public class MeasurementBuilder
{
    private DistanceMeasurement _measurement = new();

    public MeasurementBuilder WithDistance(double distance)
    {
        _measurement.Distance = distance;
        return this;
    }

    public MeasurementBuilder WithUnit(string unit)
    {
        _measurement.Unit = unit;
        return this;
    }

    public DistanceMeasurement Build() => _measurement;
}

// Usage
[TestMethod]
public void Test()
{
    var measurement = new MeasurementBuilder()
        .WithDistance(42.5)
        .WithUnit("pixels")
        .Build();
}
```

## Test Base Classes

### ViewModelTestBase

Located at `MagickCrop.Tests/Base/ViewModelTestBase.cs`

Provides:
- INotifyPropertyChanged verification helpers
- Mock service fixtures
- Property change tracking

### ServiceTestBase

Located at `MagickCrop.Tests/Base/ServiceTestBase.cs`

Provides:
- Test container setup
- Common test utilities
- Mock creation helpers

### IntegrationTestBase

Located at `MagickCrop.Tests/Base/IntegrationTestBase.cs`

Provides:
- Full application setup
- Test project creation
- Test data loading

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "ClassName=MeasurementViewModelTests"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

## Examples in Codebase

Real test examples:

- **ViewModel Tests**: `MagickCrop.Tests/ViewModels/MainWindowViewModelTests.cs`
- **Service Tests**: `MagickCrop.Tests/Services/RecentProjectsServiceTests.cs`
- **Mock Examples**: `MagickCrop.Tests/Mocks/MockFileDialogService.cs`
```

### Validation Checklist for 20j

- [ ] TESTING_PATTERNS.md created in MagickCrop.Tests/
- [ ] Includes unit test patterns with examples
- [ ] Includes integration test patterns with examples
- [ ] Mocking strategies documented with code examples
- [ ] ViewModel testing pattern documented
- [ ] Service testing pattern documented
- [ ] Async testing patterns documented
- [ ] Assertion best practices included
- [ ] Test naming conventions documented
- [ ] Test Data Builder pattern shown
- [ ] References to actual test files in codebase
- [ ] Running tests section included
- [ ] All existing tests still pass
- [ ] Documentation can be built/rendered

---

## Post-Migration Recommendations

Once unit testing is established:

1. **Expand Test Coverage**
   - Aim for 80%+ code coverage on ViewModels
   - Add tests for all service implementations
   - Focus on edge cases and error scenarios

2. **Integration Testing**
   - Create workflow tests (file load → measure → save)
   - Add UI automation tests
   - Test file format compatibility

3. **Performance Testing**
   - Profile large image handling
   - Benchmark measurement calculations
   - Identify performance bottlenecks

4. **Continuous Integration**
   - Add GitHub Actions workflow for tests
   - Block PRs on test failures
   - Generate coverage reports

---

## Validation Checklist

- [ ] Test project created and configured
- [ ] All NuGet packages installed
- [ ] Test structure organized
- [ ] Solution builds successfully
- [ ] Tests can be discovered and run
- [ ] No compiler errors or warnings

---

## Next Steps

1. Implement mock services (Step 20b)
2. Create test base classes (Step 20c)
3. Write initial ViewModel tests (Step 20d+)
4. Set up CI/CD integration
5. Expand to full test suite

---

*Unit testing foundation for production-ready MVVM application*
