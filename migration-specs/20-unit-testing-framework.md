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
