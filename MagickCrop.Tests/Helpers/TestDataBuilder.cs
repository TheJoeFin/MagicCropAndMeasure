using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;

namespace MagickCrop.Tests.Helpers;

/// <summary>
/// Fluent builder for creating test data objects with sensible defaults.
/// Allows tests to create realistic data objects with minimal boilerplate.
/// </summary>
public class TestDataBuilder
{
    /// <summary>
    /// Creates and returns a PackageMetadata object with default test values.
    /// </summary>
    /// <returns>A new PackageMetadata instance with default values.</returns>
    public static PackageMetadata BuildPackageMetadata()
    {
        return new PackageMetadata
        {
            FormatVersion = 1,
            CreationDate = DateTime.Now,
            OriginalFilename = "test_image.png",
            Notes = "Test notes",
            ProjectId = Guid.NewGuid().ToString(),
            LastModified = DateTime.Now,
            OriginalImageSize = new Size(800, 600),
            CurrentImageSize = new Size(800, 600)
        };
    }

    /// <summary>
    /// Creates a builder for PackageMetadata with fluent configuration.
    /// </summary>
    /// <returns>A new PackageMetadataBuilder instance.</returns>
    public static PackageMetadataBuilder BuildPackageMetadataWith()
    {
        return new PackageMetadataBuilder();
    }

    /// <summary>
    /// Creates and returns a MeasurementCollection object with default test values.
    /// </summary>
    /// <returns>A new MeasurementCollection instance with default empty collections.</returns>
    public static MeasurementCollection BuildMeasurementCollection()
    {
        return new MeasurementCollection
        {
            DistanceMeasurements = [],
            AngleMeasurements = [],
            VerticalLines = [],
            HorizontalLines = [],
            RectangleMeasurements = [],
            CircleMeasurements = [],
            PolygonMeasurements = [],
            InkStrokes = [],
            StrokeInfos = [],
            GlobalScaleFactor = 1.0,
            GlobalUnits = "pixels"
        };
    }

    /// <summary>
    /// Creates a builder for MeasurementCollection with fluent configuration.
    /// </summary>
    /// <returns>A new MeasurementCollectionBuilder instance.</returns>
    public static MeasurementCollectionBuilder BuildMeasurementCollectionWith()
    {
        return new MeasurementCollectionBuilder();
    }

    /// <summary>
    /// Fluent builder for PackageMetadata objects.
    /// </summary>
    public class PackageMetadataBuilder
    {
        private readonly PackageMetadata _metadata = BuildPackageMetadata();

        /// <summary>
        /// Sets the project name / filename.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <returns>This builder instance for chaining.</returns>
        public PackageMetadataBuilder WithFileName(string fileName)
        {
            _metadata.OriginalFilename = fileName;
            return this;
        }

        /// <summary>
        /// Sets the image dimensions.
        /// </summary>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <returns>This builder instance for chaining.</returns>
        public PackageMetadataBuilder WithImageDimensions(double width, double height)
        {
            _metadata.OriginalImageSize = new Size(width, height);
            _metadata.CurrentImageSize = new Size(width, height);
            return this;
        }

        /// <summary>
        /// Sets the creation date.
        /// </summary>
        /// <param name="createdAt">The creation date and time.</param>
        /// <returns>This builder instance for chaining.</returns>
        public PackageMetadataBuilder WithCreatedAt(DateTime createdAt)
        {
            _metadata.CreationDate = createdAt;
            return this;
        }

        /// <summary>
        /// Sets the modification date.
        /// </summary>
        /// <param name="modifiedAt">The modification date and time.</param>
        /// <returns>This builder instance for chaining.</returns>
        public PackageMetadataBuilder WithModifiedAt(DateTime modifiedAt)
        {
            _metadata.LastModified = modifiedAt;
            return this;
        }

        /// <summary>
        /// Sets the notes.
        /// </summary>
        /// <param name="notes">The notes content.</param>
        /// <returns>This builder instance for chaining.</returns>
        public PackageMetadataBuilder WithNotes(string notes)
        {
            _metadata.Notes = notes;
            return this;
        }

        /// <summary>
        /// Builds and returns the PackageMetadata instance.
        /// </summary>
        /// <returns>The configured PackageMetadata object.</returns>
        public PackageMetadata Build()
        {
            return _metadata;
        }
    }

    /// <summary>
    /// Fluent builder for MeasurementCollection objects.
    /// </summary>
    public class MeasurementCollectionBuilder
    {
        private readonly MeasurementCollection _collection = BuildMeasurementCollection();

        /// <summary>
        /// Adds distance measurements to the collection.
        /// </summary>
        /// <param name="measurements">The distance measurements to add.</param>
        /// <returns>This builder instance for chaining.</returns>
        public MeasurementCollectionBuilder WithDistanceMeasurements(
            params DistanceMeasurementControlDto[] measurements)
        {
            foreach (var measurement in measurements)
            {
                _collection.DistanceMeasurements.Add(measurement);
            }
            return this;
        }

        /// <summary>
        /// Adds angle measurements to the collection.
        /// </summary>
        /// <param name="measurements">The angle measurements to add.</param>
        /// <returns>This builder instance for chaining.</returns>
        public MeasurementCollectionBuilder WithAngleMeasurements(
            params AngleMeasurementControlDto[] measurements)
        {
            foreach (var measurement in measurements)
            {
                _collection.AngleMeasurements.Add(measurement);
            }
            return this;
        }

        /// <summary>
        /// Adds rectangle measurements to the collection.
        /// </summary>
        /// <param name="measurements">The rectangle measurements to add.</param>
        /// <returns>This builder instance for chaining.</returns>
        public MeasurementCollectionBuilder WithRectangleMeasurements(
            params RectangleMeasurementControlDto[] measurements)
        {
            foreach (var measurement in measurements)
            {
                _collection.RectangleMeasurements.Add(measurement);
            }
            return this;
        }

        /// <summary>
        /// Adds circle measurements to the collection.
        /// </summary>
        /// <param name="measurements">The circle measurements to add.</param>
        /// <returns>This builder instance for chaining.</returns>
        public MeasurementCollectionBuilder WithCircleMeasurements(
            params CircleMeasurementControlDto[] measurements)
        {
            foreach (var measurement in measurements)
            {
                _collection.CircleMeasurements.Add(measurement);
            }
            return this;
        }

        /// <summary>
        /// Adds polygon measurements to the collection.
        /// </summary>
        /// <param name="measurements">The polygon measurements to add.</param>
        /// <returns>This builder instance for chaining.</returns>
        public MeasurementCollectionBuilder WithPolygonMeasurements(
            params PolygonMeasurementControlDto[] measurements)
        {
            foreach (var measurement in measurements)
            {
                _collection.PolygonMeasurements.Add(measurement);
            }
            return this;
        }

        /// <summary>
        /// Adds ink strokes to the collection.
        /// </summary>
        /// <param name="strokes">The ink strokes to add.</param>
        /// <returns>This builder instance for chaining.</returns>
        public MeasurementCollectionBuilder WithInkStrokes(
            params StrokeDto[] strokes)
        {
            foreach (var stroke in strokes)
            {
                _collection.InkStrokes.Add(stroke);
            }
            return this;
        }

        /// <summary>
        /// Sets the global scale factor.
        /// </summary>
        /// <param name="scaleFactor">The scale factor to apply to all measurements.</param>
        /// <returns>This builder instance for chaining.</returns>
        public MeasurementCollectionBuilder WithGlobalScaleFactor(double scaleFactor)
        {
            _collection.GlobalScaleFactor = scaleFactor;
            return this;
        }

        /// <summary>
        /// Sets the global measurement units.
        /// </summary>
        /// <param name="units">The units to apply to all measurements.</param>
        /// <returns>This builder instance for chaining.</returns>
        public MeasurementCollectionBuilder WithGlobalUnits(string units)
        {
            _collection.GlobalUnits = units;
            return this;
        }

        /// <summary>
        /// Builds and returns the MeasurementCollection instance.
        /// </summary>
        /// <returns>The configured MeasurementCollection object.</returns>
        public MeasurementCollection Build()
        {
            return _collection;
        }
    }
}
