using MagickCrop.Services.Interfaces;
using MagickCrop.Tests.Fixtures;
using MagickCrop.Tests.Helpers;
using MagickCrop.ViewModels.Base;

namespace MagickCrop.Tests.Base;

/// <summary>
/// Base class for all ViewModel unit tests.
/// Provides common setup, teardown, and helper methods for testing ViewModels.
/// </summary>
[TestClass]
public abstract class ViewModelTestBase : IDisposable
{
    /// <summary>
    /// Gets the test service fixture providing mock service instances.
    /// </summary>
    protected TestServiceFixture ServiceFixture { get; private set; } = null!;

    /// <summary>
    /// Gets the property change helper for verifying INotifyPropertyChanged notifications.
    /// </summary>
    protected PropertyChangedHelper PropertyChangedHelper { get; private set; } = null!;

    /// <summary>
    /// Called before each test runs. Initializes the service fixture and helpers.
    /// </summary>
    [TestInitialize]
    public virtual void TestInitialize()
    {
        ServiceFixture = new TestServiceFixture();
        PropertyChangedHelper = new PropertyChangedHelper();
    }

    /// <summary>
    /// Called after each test runs. Disposes of resources.
    /// </summary>
    [TestCleanup]
    public virtual void TestCleanup()
    {
        ServiceFixture?.Dispose();
    }

    /// <summary>
    /// Disposes of resources when the test instance is disposed.
    /// </summary>
    public void Dispose()
    {
        TestCleanup();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets a service instance from the test fixture's service provider.
    /// </summary>
    /// <typeparam name="T">The type of service to retrieve.</typeparam>
    /// <returns>The service instance, or throws if not found.</returns>
    protected T GetService<T>() where T : class
    {
        return ServiceFixture.GetService<T>();
    }

    /// <summary>
    /// Creates and returns an instance of the specified ViewModel type using the fixture's services.
    /// </summary>
    /// <typeparam name="T">The ViewModel type to create. Must be assignable from ViewModelBase.</typeparam>
    /// <returns>A new instance of the specified ViewModel type.</returns>
    /// <exception cref="ArgumentException">Thrown if T cannot be instantiated or is not a ViewModel.</exception>
    protected T CreateViewModel<T>() where T : ViewModelBase
    {
        try
        {
            // Try to get from DI container first
            return ServiceFixture.GetService<T>();
        }
        catch (InvalidOperationException)
        {
            // If not registered, try direct instantiation with common constructor patterns
            var constructors = typeof(T).GetConstructors();

            if (constructors.Length == 0)
            {
                throw new ArgumentException($"ViewModel type {typeof(T).Name} has no public constructors.");
            }

            // Try parameterless constructor first
            var parameterlessConstructor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
            if (parameterlessConstructor != null)
            {
                return (T)parameterlessConstructor.Invoke(null)!;
            }

            // Try constructor with IMessenger parameter (common in ViewModels)
            var messengerConstructor = constructors.FirstOrDefault(c =>
                c.GetParameters().Length == 1 &&
                c.GetParameters()[0].ParameterType.Name == "IMessenger");

            if (messengerConstructor != null)
            {
                var messenger = GetService<CommunityToolkit.Mvvm.Messaging.IMessenger>();
                return (T)messengerConstructor.Invoke(new object[] { messenger })!;
            }

            throw new ArgumentException(
                $"Cannot create ViewModel {typeof(T).Name}. No suitable constructor found.");
        }
    }

    /// <summary>
    /// Monitors property changes on a ViewModel and returns a tracker for assertions.
    /// </summary>
    /// <param name="viewModel">The ViewModel to monitor.</param>
    /// <returns>A PropertyChangeTracker that tracks changes on the ViewModel.</returns>
    protected PropertyChangeTracker MonitorPropertyChanges(ViewModelBase viewModel)
    {
        var tracker = PropertyChangedHelper.CreateTracker(viewModel);
        return tracker;
    }

    /// <summary>
    /// Asserts that a property changed exactly once with the expected old and new values.
    /// </summary>
    /// <param name="propertyName">The name of the property that should have changed.</param>
    /// <param name="expectedOldValue">The expected previous value.</param>
    /// <param name="expectedNewValue">The expected current value.</param>
    /// <param name="tracker">The property change tracker to check.</param>
    /// <exception cref="AssertFailedException">Thrown if the assertion fails.</exception>
    protected void AssertPropertyChanged(
        string propertyName,
        object? expectedOldValue,
        object? expectedNewValue,
        PropertyChangeTracker tracker)
    {
        var changes = tracker.GetChanges(propertyName);

        Assert.IsNotNull(changes, $"Property '{propertyName}' was never changed.");
        Assert.AreEqual(1, changes.Count, 
            $"Property '{propertyName}' changed {changes.Count} times instead of once.");

        var change = changes.First();
        Assert.AreEqual(expectedOldValue, change.OldValue,
            $"Property '{propertyName}' old value mismatch.");
        Assert.AreEqual(expectedNewValue, change.NewValue,
            $"Property '{propertyName}' new value mismatch.");
    }

    /// <summary>
    /// Asserts that a property was never changed.
    /// </summary>
    /// <param name="propertyName">The name of the property that should not have changed.</param>
    /// <param name="tracker">The property change tracker to check.</param>
    /// <exception cref="AssertFailedException">Thrown if the property was changed.</exception>
    protected void AssertPropertyNotChanged(string propertyName, PropertyChangeTracker tracker)
    {
        var changes = tracker.GetChanges(propertyName);

        Assert.IsNull(changes, 
            $"Property '{propertyName}' was changed {changes?.Count ?? 0} times but should not have changed.");
    }

    /// <summary>
    /// Asserts that a property changed at least once.
    /// </summary>
    /// <param name="propertyName">The name of the property that should have changed.</param>
    /// <param name="tracker">The property change tracker to check.</param>
    /// <exception cref="AssertFailedException">Thrown if the property never changed.</exception>
    protected void AssertPropertyChanged(string propertyName, PropertyChangeTracker tracker)
    {
        var changes = tracker.GetChanges(propertyName);

        Assert.IsNotNull(changes, 
            $"Property '{propertyName}' was never changed.");
        Assert.IsTrue(changes.Count > 0,
            $"Property '{propertyName}' was never changed.");
    }

    /// <summary>
    /// Gets the mock recent projects service.
    /// </summary>
    protected MagickCrop.Tests.Mocks.MockRecentProjectsService GetMockRecentProjectsService()
    {
        return GetService<IRecentProjectsService>() as MagickCrop.Tests.Mocks.MockRecentProjectsService
            ?? throw new InvalidOperationException("Recent projects service is not the expected mock type.");
    }

    /// <summary>
    /// Gets the mock file dialog service.
    /// </summary>
    protected MagickCrop.Tests.Mocks.MockFileDialogService GetMockFileDialogService()
    {
        return GetService<IFileDialogService>() as MagickCrop.Tests.Mocks.MockFileDialogService
            ?? throw new InvalidOperationException("File dialog service is not the expected mock type.");
    }

    /// <summary>
    /// Gets the mock clipboard service.
    /// </summary>
    protected MagickCrop.Tests.Mocks.MockClipboardService GetMockClipboardService()
    {
        return GetService<IClipboardService>() as MagickCrop.Tests.Mocks.MockClipboardService
            ?? throw new InvalidOperationException("Clipboard service is not the expected mock type.");
    }

    /// <summary>
    /// Gets the mock image processing service.
    /// </summary>
    protected MagickCrop.Tests.Mocks.MockImageProcessingService GetMockImageProcessingService()
    {
        return GetService<IImageProcessingService>() as MagickCrop.Tests.Mocks.MockImageProcessingService
            ?? throw new InvalidOperationException("Image processing service is not the expected mock type.");
    }

    /// <summary>
    /// Gets the mock navigation service.
    /// </summary>
    protected MagickCrop.Tests.Mocks.MockNavigationService GetMockNavigationService()
    {
        return GetService<INavigationService>() as MagickCrop.Tests.Mocks.MockNavigationService
            ?? throw new InvalidOperationException("Navigation service is not the expected mock type.");
    }

    /// <summary>
    /// Gets the mock window factory.
    /// </summary>
    protected MagickCrop.Tests.Mocks.MockWindowFactory GetMockWindowFactory()
    {
        return GetService<IWindowFactory>() as MagickCrop.Tests.Mocks.MockWindowFactory
            ?? throw new InvalidOperationException("Window factory is not the expected mock type.");
    }

    /// <summary>
    /// Gets the mock theme service.
    /// </summary>
    protected MagickCrop.Tests.Mocks.MockThemeService GetMockThemeService()
    {
        return GetService<IThemeService>() as MagickCrop.Tests.Mocks.MockThemeService
            ?? throw new InvalidOperationException("Theme service is not the expected mock type.");
    }
}
