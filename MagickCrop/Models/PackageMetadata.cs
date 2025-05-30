namespace MagickCrop.Models;

/// <summary>
/// Metadata about a MagickCrop measurement package
/// </summary>
public class PackageMetadata
{
    /// <summary>
    /// Format version to ensure compatibility
    /// </summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>
    /// Date the package was created
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Original filename of the source image (for reference only)
    /// </summary>
    public string? OriginalFilename { get; set; }

    /// <summary>
    /// Notes or description about the measurements
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Unique identifier for the project
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Date the package was last modified
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.Now;
}
