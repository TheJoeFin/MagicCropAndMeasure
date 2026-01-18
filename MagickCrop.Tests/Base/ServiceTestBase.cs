using System.Collections.Generic;
using MagickCrop.Tests.Fixtures;

namespace MagickCrop.Tests.Base;

/// <summary>
/// Base class for all service unit tests.
/// Provides common setup, teardown, and helper methods for testing services.
/// </summary>
[TestClass]
public abstract class ServiceTestBase : IDisposable
{
    /// <summary>
    /// Gets the test service fixture providing mock service instances.
    /// </summary>
    protected TestServiceFixture ServiceFixture { get; private set; } = null!;

    /// <summary>
    /// Called before each test runs. Initializes the service fixture.
    /// </summary>
    [TestInitialize]
    public virtual void TestInitialize()
    {
        ServiceFixture = new TestServiceFixture();
    }

    /// <summary>
    /// Called after each test runs. Disposes of resources.
    /// </summary>
    [TestCleanup]
    public virtual void TestCleanup()
    {
        CleanupTemporaryFiles();
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
    /// Asserts that an async method throws an exception of the specified type.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The async action that should throw.</param>
    /// <returns>The thrown exception for further assertions.</returns>
    protected async Task<TException> AssertThrowsAsync<TException>(Func<Task> action) 
        where TException : Exception
    {
        try
        {
            await action();
            throw new AssertFailedException($"Expected {typeof(TException).Name} to be thrown, but no exception was thrown.");
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertFailedException(
                $"Expected {typeof(TException).Name}, but {ex.GetType().Name} was thrown instead.",
                ex);
        }
    }

    /// <summary>
    /// Asserts that an async method throws an exception of the specified type with a specific message.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The async action that should throw.</param>
    /// <param name="expectedMessage">The expected exception message.</param>
    /// <returns>The thrown exception for further assertions.</returns>
    protected async Task<TException> AssertThrowsAsync<TException>(
        Func<Task> action,
        string expectedMessage) where TException : Exception
    {
        var exception = await AssertThrowsAsync<TException>(action);
        Assert.AreEqual(expectedMessage, exception.Message,
            $"Exception message mismatch. Expected: '{expectedMessage}', Actual: '{exception.Message}'");
        return exception;
    }

    /// <summary>
    /// Asserts that an async method with a return value throws an exception of the specified type.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <typeparam name="T">The return type of the async method.</typeparam>
    /// <param name="action">The async action that should throw.</param>
    /// <returns>The thrown exception for further assertions.</returns>
    protected async Task<TException> AssertThrowsAsync<TException, T>(Func<Task<T>> action)
        where TException : Exception
    {
        try
        {
            await action();
            throw new AssertFailedException($"Expected {typeof(TException).Name} to be thrown, but no exception was thrown.");
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertFailedException(
                $"Expected {typeof(TException).Name}, but {ex.GetType().Name} was thrown instead.",
                ex);
        }
    }

    /// <summary>
    /// Asserts that an async method with a return value throws an exception of the specified type with a specific message.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <typeparam name="T">The return type of the async method.</typeparam>
    /// <param name="action">The async action that should throw.</param>
    /// <param name="expectedMessage">The expected exception message.</param>
    /// <returns>The thrown exception for further assertions.</returns>
    protected async Task<TException> AssertThrowsAsync<TException, T>(
        Func<Task<T>> action,
        string expectedMessage) where TException : Exception
    {
        var exception = await AssertThrowsAsync<TException, T>(action);
        Assert.AreEqual(expectedMessage, exception.Message,
            $"Exception message mismatch. Expected: '{expectedMessage}', Actual: '{exception.Message}'");
        return exception;
    }

    /// <summary>
    /// Creates a temporary test file that is automatically cleaned up after the test.
    /// </summary>
    /// <param name="content">Optional content to write to the file.</param>
    /// <returns>The full path to the created temporary file.</returns>
    protected string CreateTempFile(string? content = null)
    {
        var tempPath = Path.GetTempFileName();
        
        if (content != null)
        {
            File.WriteAllText(tempPath, content);
        }

        // Register for cleanup in cleanup method
        _tempFilesToClean.Add(tempPath);

        return tempPath;
    }

    /// <summary>
    /// Creates a temporary test directory that is automatically cleaned up after the test.
    /// </summary>
    /// <returns>The full path to the created temporary directory.</returns>
    protected string CreateTempDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempPath);

        // Register for cleanup in cleanup method
        _tempDirectoriesToClean.Add(tempPath);

        return tempPath;
    }

    /// <summary>
    /// Performs cleanup of temporary files and directories created during the test.
    /// Override to call base implementation to ensure cleanup occurs.
    /// </summary>
    protected virtual void CleanupTemporaryFiles()
    {
        foreach (var file in _tempFilesToClean.Where(File.Exists))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        foreach (var dir in _tempDirectoriesToClean.Where(Directory.Exists))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _tempFilesToClean.Clear();
        _tempDirectoriesToClean.Clear();
    }

    private readonly List<string> _tempFilesToClean = [];
    private readonly List<string> _tempDirectoriesToClean = [];
}
