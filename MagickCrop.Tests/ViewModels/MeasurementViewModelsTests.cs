using System.Collections.Specialized;
using System.Windows;
using MagickCrop.Helpers;
using MagickCrop.Messages;
using MagickCrop.Tests.Base;
using MagickCrop.ViewModels.Measurements;
using CommunityToolkit.Mvvm.Messaging;

namespace MagickCrop.Tests.ViewModels;

/// <summary>
/// Comprehensive unit tests for all measurement ViewModels.
/// </summary>
public class MeasurementViewModelsTests
{
    #region DistanceMeasurementViewModel Tests

    [TestClass]
    public class DistanceMeasurementViewModelTests : ViewModelTestBase
    {
        private DistanceMeasurementViewModel _viewModel = null!;
        private CommunityToolkit.Mvvm.Messaging.IMessenger _messenger = null!;

        [TestInitialize]
        public override void TestInitialize()
        {
            base.TestInitialize();
            _messenger = new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger();
            _viewModel = new DistanceMeasurementViewModel(_messenger);
        }

        [TestMethod]
        public void Constructor_InitializesWithDefaultValues()
        {
            Assert.IsNotNull(_viewModel.Id);
            Assert.AreNotEqual(Guid.Empty, _viewModel.Id);
            Assert.AreEqual(0, _viewModel.PixelLength);
            Assert.AreEqual("Distance", _viewModel.MeasurementType);
            Assert.AreEqual(1.0, _viewModel.ScaleFactor);
            Assert.AreEqual("px", _viewModel.Units);
        }

        [TestMethod]
        public void PixelLength_CalculatesCorrectlyForHorizontalLine()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(10, 0);

            Assert.AreEqual(10.0, _viewModel.PixelLength, 0.001);
        }

        [TestMethod]
        public void PixelLength_CalculatesCorrectlyForVerticalLine()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(0, 10);

            Assert.AreEqual(10.0, _viewModel.PixelLength, 0.001);
        }

        [TestMethod]
        public void PixelLength_CalculatesCorrectlyForDiagonalLine()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(3, 4);

            Assert.AreEqual(5.0, _viewModel.PixelLength, 0.001);
        }

        [TestMethod]
        public void PixelLength_CalculatesCorrectlyForZeroLength()
        {
            _viewModel.StartPoint = new Point(5, 5);
            _viewModel.EndPoint = new Point(5, 5);

            Assert.AreEqual(0, _viewModel.PixelLength, 0.001);
        }

        [TestMethod]
        public void Angle_CalculatesCorrectlyForHorizontalLine()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(10, 0);

            Assert.AreEqual(0, _viewModel.Angle, 0.001);
        }

        [TestMethod]
        public void Angle_CalculatesCorrectlyForVerticalLineUp()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(0, -10);

            Assert.AreEqual(-90, _viewModel.Angle, 0.001);
        }

        [TestMethod]
        public void Angle_CalculatesCorrectlyForVerticalLineDown()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(0, 10);

            Assert.AreEqual(90, _viewModel.Angle, 0.001);
        }

        [TestMethod]
        public void Angle_CalculatesCorrectlyFor45DegreeAngle()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(10, 10);

            Assert.AreEqual(45, _viewModel.Angle, 0.001);
        }

        [TestMethod]
        public void MidPoint_CalculatesCorrectly()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(10, 10);

            var midpoint = _viewModel.MidPoint;
            Assert.AreEqual(5, midpoint.X);
            Assert.AreEqual(5, midpoint.Y);
        }

        [TestMethod]
        public void MidPoint_CalculatesCorrectlyWithNegativeCoordinates()
        {
            _viewModel.StartPoint = new Point(-10, -10);
            _viewModel.EndPoint = new Point(10, 10);

            var midpoint = _viewModel.MidPoint;
            Assert.AreEqual(0, midpoint.X);
            Assert.AreEqual(0, midpoint.Y);
        }

        [TestMethod]
        public void DisplayText_FormatsCorrectlyInPixels()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(10, 0);
            _viewModel.Units = "px";
            _viewModel.ScaleFactor = 1.0;

            Assert.IsTrue(_viewModel.DisplayText.Contains("10.0"));
            Assert.IsTrue(_viewModel.DisplayText.Contains("px"));
        }

        [TestMethod]
        public void DisplayText_UpdatesWhenScaleFactorChanges()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(10, 0);
            _viewModel.Units = "cm";
            _viewModel.ScaleFactor = 2.0;

            Assert.IsTrue(_viewModel.DisplayText.Contains("20.00"));
            Assert.IsTrue(_viewModel.DisplayText.Contains("cm"));
        }

        [TestMethod]
        public void PixelLength_UpdatesWhenStartPointChanges()
        {
            _viewModel.EndPoint = new Point(10, 10);
            var initialLength = _viewModel.PixelLength;

            _viewModel.StartPoint = new Point(5, 5);

            Assert.AreNotEqual(initialLength, _viewModel.PixelLength);
        }

        [TestMethod]
        public void PixelLength_UpdatesWhenEndPointChanges()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(10, 10);
            var initialLength = _viewModel.PixelLength;

            _viewModel.EndPoint = new Point(20, 20);

            Assert.IsTrue(_viewModel.PixelLength > initialLength);
        }

        [TestMethod]
        public void ScaleFactorChangedMessage_UpdatesScaleFactorAndUnits()
        {
            _viewModel.StartPoint = new Point(0, 0);
            _viewModel.EndPoint = new Point(100, 0);

            var message = new ScaleFactorChangedMessage(2.5, "inches");
            _messenger.Send(message);

            Assert.AreEqual(2.5, _viewModel.ScaleFactor);
            Assert.AreEqual("inches", _viewModel.Units);
        }

        [TestMethod]
        public void ColorProperty_CanBeChanged()
        {
            var newColor = System.Windows.Media.Colors.Red;
            _viewModel.Color = newColor;

            Assert.AreEqual(newColor, _viewModel.Color);
        }

        [TestMethod]
        public void StrokeThickness_CanBeChanged()
        {
            _viewModel.StrokeThickness = 5.0;

            Assert.AreEqual(5.0, _viewModel.StrokeThickness);
        }

        [TestMethod]
        public void IsSelected_CanBeChanged()
        {
            Assert.IsFalse(_viewModel.IsSelected);

            _viewModel.IsSelected = true;

            Assert.IsTrue(_viewModel.IsSelected);
        }
    }

    #endregion

    #region AngleMeasurementViewModel Tests

    [TestClass]
    public class AngleMeasurementViewModelTests : ViewModelTestBase
    {
        private AngleMeasurementViewModel _viewModel = null!;
        private CommunityToolkit.Mvvm.Messaging.IMessenger _messenger = null!;

        [TestInitialize]
        public override void TestInitialize()
        {
            base.TestInitialize();
            _messenger = new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger();
            _viewModel = new AngleMeasurementViewModel(_messenger);
        }

        [TestMethod]
        public void Constructor_InitializesWithDefaultValues()
        {
            Assert.IsNotNull(_viewModel.Id);
            Assert.AreEqual("Angle", _viewModel.MeasurementType);
            Assert.AreEqual(0, _viewModel.AngleDegrees);
        }

        [TestMethod]
        public void AngleDegrees_CalculatesCorrectly_RightAngle()
        {
            _viewModel.Vertex = new Point(0, 0);
            _viewModel.Point1 = new Point(10, 0);
            _viewModel.Point2 = new Point(0, 10);

            Assert.AreEqual(90, _viewModel.AngleDegrees, 0.1);
        }

        [TestMethod]
        public void AngleDegrees_CalculatesCorrectly_StraightAngle()
        {
            _viewModel.Vertex = new Point(0, 0);
            _viewModel.Point1 = new Point(10, 0);
            _viewModel.Point2 = new Point(-10, 0);

            Assert.AreEqual(180, _viewModel.AngleDegrees, 0.1);
        }

        [TestMethod]
        public void AngleDegrees_CalculatesCorrectly_45DegreeAngle()
        {
            _viewModel.Vertex = new Point(0, 0);
            _viewModel.Point1 = new Point(10, 0);
            _viewModel.Point2 = new Point(10, 10);

            Assert.AreEqual(45, _viewModel.AngleDegrees, 0.1);
        }

        [TestMethod]
        public void AngleDegrees_HandlesZeroVectorPoint1()
        {
            _viewModel.Vertex = new Point(5, 5);
            _viewModel.Point1 = new Point(5, 5); // Zero vector
            _viewModel.Point2 = new Point(15, 5);

            Assert.AreEqual(0, _viewModel.AngleDegrees);
        }

        [TestMethod]
        public void AngleDegrees_HandlesZeroVectorPoint2()
        {
            _viewModel.Vertex = new Point(5, 5);
            _viewModel.Point1 = new Point(15, 5);
            _viewModel.Point2 = new Point(5, 5); // Zero vector

            Assert.AreEqual(0, _viewModel.AngleDegrees);
        }

        [TestMethod]
        public void AngleDegrees_HandlesParallelVectorsWithOppositeDirections()
        {
            _viewModel.Vertex = new Point(0, 0);
            _viewModel.Point1 = new Point(10, 0);
            _viewModel.Point2 = new Point(-5, 0);

            Assert.AreEqual(180, _viewModel.AngleDegrees, 0.1);
        }

        [TestMethod]
        public void DisplayText_FormatsAngleCorrectly()
        {
            _viewModel.Vertex = new Point(0, 0);
            _viewModel.Point1 = new Point(10, 0);
            _viewModel.Point2 = new Point(0, 10);

            Assert.IsTrue(_viewModel.DisplayText.Contains("90.0"));
            Assert.IsTrue(_viewModel.DisplayText.Contains("°"));
        }

        [TestMethod]
        public void AngleDegrees_UpdatesWhenVertexChanges()
        {
            _viewModel.Point1 = new Point(10, 0);
            _viewModel.Point2 = new Point(0, 10);
            _viewModel.Vertex = new Point(0, 0);
            var initialAngle = _viewModel.AngleDegrees;

            _viewModel.Vertex = new Point(5, 5);

            // When vertex changes, the angle between the rays should change
            Assert.AreNotEqual(initialAngle, _viewModel.AngleDegrees, 0.1);
            // New angle should be 180 degrees (opposite vectors)
            Assert.AreEqual(180, _viewModel.AngleDegrees, 0.1);
        }

        [TestMethod]
        public void AngleDegrees_UpdatesWhenPoint1Changes()
        {
            _viewModel.Vertex = new Point(0, 0);
            _viewModel.Point1 = new Point(10, 0);
            _viewModel.Point2 = new Point(0, 10);
            var initialAngle = _viewModel.AngleDegrees;

            _viewModel.Point1 = new Point(7.07, -7.07); // Approximately 45 degrees

            Assert.AreNotEqual(initialAngle, _viewModel.AngleDegrees);
        }

        [TestMethod]
        public void AngleDegrees_IsAlwaysPositive()
        {
            _viewModel.Vertex = new Point(0, 0);
            _viewModel.Point1 = new Point(10, 0);
            _viewModel.Point2 = new Point(-10, -10);

            Assert.IsTrue(_viewModel.AngleDegrees >= 0);
            Assert.IsTrue(_viewModel.AngleDegrees <= 180);
        }

        [TestMethod]
        public void ScaleFactorChangedMessage_DoesNotAffectAngleCalculation()
        {
            _viewModel.Vertex = new Point(0, 0);
            _viewModel.Point1 = new Point(10, 0);
            _viewModel.Point2 = new Point(0, 10);
            var initialAngle = _viewModel.AngleDegrees;

            var message = new ScaleFactorChangedMessage(5.0, "cm");
            _messenger.Send(message);

            Assert.AreEqual(initialAngle, _viewModel.AngleDegrees, 0.001);
        }
    }

    #endregion

    #region CircleMeasurementViewModel Tests

    [TestClass]
    public class CircleMeasurementViewModelTests : ViewModelTestBase
    {
        private CircleMeasurementViewModel _viewModel = null!;
        private CommunityToolkit.Mvvm.Messaging.IMessenger _messenger = null!;

        [TestInitialize]
        public override void TestInitialize()
        {
            base.TestInitialize();
            _messenger = new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger();
            _viewModel = new CircleMeasurementViewModel(_messenger);
        }

        [TestMethod]
        public void Constructor_InitializesWithDefaultValues()
        {
            Assert.IsNotNull(_viewModel.Id);
            Assert.AreEqual("Circle", _viewModel.MeasurementType);
            Assert.AreEqual(0, _viewModel.Radius);
            Assert.AreEqual(0, _viewModel.Diameter);
            Assert.AreEqual(0, _viewModel.Circumference);
            Assert.AreEqual(0, _viewModel.Area);
        }

        [TestMethod]
        public void Radius_CalculatesCorrectly()
        {
            _viewModel.CenterPoint = new Point(0, 0);
            _viewModel.EdgePoint = new Point(5, 0);

            Assert.AreEqual(5.0, _viewModel.Radius, 0.001);
        }

        [TestMethod]
        public void Radius_CalculatesCorrectlyForDiagonalEdgePoint()
        {
            _viewModel.CenterPoint = new Point(0, 0);
            _viewModel.EdgePoint = new Point(3, 4);

            Assert.AreEqual(5.0, _viewModel.Radius, 0.001);
        }

        [TestMethod]
        public void Radius_CalculatesCorrectlyForZeroRadius()
        {
            _viewModel.CenterPoint = new Point(5, 5);
            _viewModel.EdgePoint = new Point(5, 5);

            Assert.AreEqual(0, _viewModel.Radius, 0.001);
        }

        [TestMethod]
        public void Diameter_IsDoubleTheRadius()
        {
            _viewModel.CenterPoint = new Point(0, 0);
            _viewModel.EdgePoint = new Point(5, 0);

            Assert.AreEqual(10.0, _viewModel.Diameter, 0.001);
        }

        [TestMethod]
        public void Circumference_CalculatesCorrectly()
        {
            _viewModel.CenterPoint = new Point(0, 0);
            _viewModel.EdgePoint = new Point(5, 0);

            var expected = Math.PI * 10;
            Assert.AreEqual(expected, _viewModel.Circumference, 0.001);
        }

        [TestMethod]
        public void Area_CalculatesCorrectly()
        {
            _viewModel.CenterPoint = new Point(0, 0);
            _viewModel.EdgePoint = new Point(5, 0);

            var expected = Math.PI * 25;
            Assert.AreEqual(expected, _viewModel.Area, 0.001);
        }

        [TestMethod]
        public void Area_IsZeroForZeroRadius()
        {
            _viewModel.CenterPoint = new Point(5, 5);
            _viewModel.EdgePoint = new Point(5, 5);

            Assert.AreEqual(0, _viewModel.Area, 0.001);
        }

        [TestMethod]
        public void CircleCanvasLeft_CalculatesCorrectly()
        {
            _viewModel.CenterPoint = new Point(10, 10);
            _viewModel.EdgePoint = new Point(15, 10);

            Assert.AreEqual(5, _viewModel.CircleCanvasLeft, 0.001);
        }

        [TestMethod]
        public void CircleCanvasTop_CalculatesCorrectly()
        {
            _viewModel.CenterPoint = new Point(10, 10);
            _viewModel.EdgePoint = new Point(10, 15);

            Assert.AreEqual(5, _viewModel.CircleCanvasTop, 0.001);
        }

        [TestMethod]
        public void CanvasPositioning_PlacesCircleCorrectly()
        {
            _viewModel.CenterPoint = new Point(20, 30);
            _viewModel.EdgePoint = new Point(25, 30);

            var expectedLeft = 20 - 5; // Center - Radius
            var expectedTop = 30 - 5;

            Assert.AreEqual(expectedLeft, _viewModel.CircleCanvasLeft, 0.001);
            Assert.AreEqual(expectedTop, _viewModel.CircleCanvasTop, 0.001);
        }

        [TestMethod]
        public void DisplayText_IncludesAllMeasurements()
        {
            _viewModel.CenterPoint = new Point(0, 0);
            _viewModel.EdgePoint = new Point(5, 0);
            _viewModel.Units = "px";

            Assert.IsTrue(_viewModel.DisplayText.Contains("R:"));
            Assert.IsTrue(_viewModel.DisplayText.Contains("D:"));
            Assert.IsTrue(_viewModel.DisplayText.Contains("C:"));
            Assert.IsTrue(_viewModel.DisplayText.Contains("A:"));
        }

        [TestMethod]
        public void AllMeasurements_UpdateWhenCenterPointChanges()
        {
            _viewModel.EdgePoint = new Point(10, 10);
            _viewModel.CenterPoint = new Point(0, 0);
            var initialRadius = _viewModel.Radius;

            _viewModel.CenterPoint = new Point(5, 5);

            // When center point changes, the distance to edge changes, so radius should change
            Assert.AreNotEqual(initialRadius, _viewModel.Radius);
            Assert.AreEqual(Math.Sqrt(50), _viewModel.Radius, 0.001);
        }

        [TestMethod]
        public void AllMeasurements_UpdateWhenEdgePointChanges()
        {
            _viewModel.CenterPoint = new Point(0, 0);
            _viewModel.EdgePoint = new Point(5, 0);
            var initialRadius = _viewModel.Radius;

            _viewModel.EdgePoint = new Point(10, 0);

            Assert.IsTrue(_viewModel.Radius > initialRadius);
        }

        [TestMethod]
        public void DisplayText_UpdatesWithScaleFactor()
        {
            _viewModel.CenterPoint = new Point(0, 0);
            _viewModel.EdgePoint = new Point(100, 0);
            _viewModel.Units = "cm";
            _viewModel.ScaleFactor = 2.0;

            var oldText = _viewModel.DisplayText;
            _viewModel.ScaleFactor = 4.0;

            Assert.AreNotEqual(oldText, _viewModel.DisplayText);
        }
    }

    #endregion

    #region RectangleMeasurementViewModel Tests

    [TestClass]
    public class RectangleMeasurementViewModelTests : ViewModelTestBase
    {
        private RectangleMeasurementViewModel _viewModel = null!;
        private CommunityToolkit.Mvvm.Messaging.IMessenger _messenger = null!;

        [TestInitialize]
        public override void TestInitialize()
        {
            base.TestInitialize();
            _messenger = new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger();
            _viewModel = new RectangleMeasurementViewModel(_messenger);
        }

        [TestMethod]
        public void Constructor_InitializesWithDefaultValues()
        {
            Assert.IsNotNull(_viewModel.Id);
            Assert.AreEqual("Rectangle", _viewModel.MeasurementType);
            Assert.AreEqual(0, _viewModel.Width);
            Assert.AreEqual(0, _viewModel.Height);
            Assert.AreEqual(0, _viewModel.Perimeter);
            Assert.AreEqual(0, _viewModel.Area);
        }

        [TestMethod]
        public void Width_CalculatesCorrectly()
        {
            _viewModel.TopLeft = new Point(0, 0);
            _viewModel.BottomRight = new Point(10, 5);

            Assert.AreEqual(10.0, _viewModel.Width, 0.001);
        }

        [TestMethod]
        public void Width_CalculatesCorrectlyWhenRectangleIsReverse()
        {
            _viewModel.TopLeft = new Point(10, 0);
            _viewModel.BottomRight = new Point(0, 5);

            Assert.AreEqual(10.0, _viewModel.Width, 0.001);
        }

        [TestMethod]
        public void Height_CalculatesCorrectly()
        {
            _viewModel.TopLeft = new Point(0, 0);
            _viewModel.BottomRight = new Point(10, 15);

            Assert.AreEqual(15.0, _viewModel.Height, 0.001);
        }

        [TestMethod]
        public void Height_CalculatesCorrectlyWhenRectangleIsReverse()
        {
            _viewModel.TopLeft = new Point(0, 15);
            _viewModel.BottomRight = new Point(10, 0);

            Assert.AreEqual(15.0, _viewModel.Height, 0.001);
        }

        [TestMethod]
        public void Perimeter_CalculatesCorrectly()
        {
            _viewModel.TopLeft = new Point(0, 0);
            _viewModel.BottomRight = new Point(10, 5);

            Assert.AreEqual(30.0, _viewModel.Perimeter, 0.001);
        }

        [TestMethod]
        public void Area_CalculatesCorrectly()
        {
            _viewModel.TopLeft = new Point(0, 0);
            _viewModel.BottomRight = new Point(10, 5);

            Assert.AreEqual(50.0, _viewModel.Area, 0.001);
        }

        [TestMethod]
        public void Area_IsZeroForZeroWidthOrHeight()
        {
            _viewModel.TopLeft = new Point(5, 5);
            _viewModel.BottomRight = new Point(5, 10);

            Assert.AreEqual(0, _viewModel.Area, 0.001);
        }

        [TestMethod]
        public void Bounds_CalculatesCorrectly()
        {
            _viewModel.TopLeft = new Point(0, 0);
            _viewModel.BottomRight = new Point(10, 10);

            var bounds = _viewModel.Bounds;
            Assert.AreEqual(0, bounds.Left);
            Assert.AreEqual(0, bounds.Top);
            Assert.AreEqual(10, bounds.Width);
            Assert.AreEqual(10, bounds.Height);
        }

        [TestMethod]
        public void Bounds_HandlesReversedCorners()
        {
            _viewModel.TopLeft = new Point(10, 10);
            _viewModel.BottomRight = new Point(0, 0);

            var bounds = _viewModel.Bounds;
            Assert.IsTrue(bounds.Width >= 0);
            Assert.IsTrue(bounds.Height >= 0);
        }

        [TestMethod]
        public void DisplayText_IncludesWidthHeightAndArea()
        {
            _viewModel.TopLeft = new Point(0, 0);
            _viewModel.BottomRight = new Point(10, 5);
            _viewModel.Units = "px";

            Assert.IsTrue(_viewModel.DisplayText.Contains("10.0"));
            Assert.IsTrue(_viewModel.DisplayText.Contains("5.0"));
            Assert.IsTrue(_viewModel.DisplayText.Contains("Area:"));
        }

        [TestMethod]
        public void DisplayText_UpdatesWhenDimensionsChange()
        {
            _viewModel.TopLeft = new Point(0, 0);
            _viewModel.BottomRight = new Point(10, 5);
            var initialText = _viewModel.DisplayText;

            _viewModel.BottomRight = new Point(20, 15);

            Assert.AreNotEqual(initialText, _viewModel.DisplayText);
        }

        [TestMethod]
        public void PerimeterAndArea_UpdateWhenTopLeftChanges()
        {
            _viewModel.BottomRight = new Point(10, 10);
            _viewModel.TopLeft = new Point(0, 0);
            var initialPerimeter = _viewModel.Perimeter;

            _viewModel.TopLeft = new Point(2, 2);

            // When TopLeft changes, the rectangle dimensions change, so perimeter should change
            Assert.AreNotEqual(initialPerimeter, _viewModel.Perimeter);
            Assert.AreEqual(32, _viewModel.Perimeter, 0.001);
        }

        [TestMethod]
        public void PerimeterAndArea_UpdateWhenBottomRightChanges()
        {
            _viewModel.TopLeft = new Point(0, 0);
            _viewModel.BottomRight = new Point(10, 10);
            var initialPerimeter = _viewModel.Perimeter;

            _viewModel.BottomRight = new Point(20, 20);

            Assert.IsTrue(_viewModel.Perimeter > initialPerimeter);
        }

        [TestMethod]
        public void DisplayText_UpdatesWithScaleFactor()
        {
            _viewModel.TopLeft = new Point(0, 0);
            _viewModel.BottomRight = new Point(100, 100);
            _viewModel.Units = "cm";
            _viewModel.ScaleFactor = 1.0;

            var oldText = _viewModel.DisplayText;
            _viewModel.ScaleFactor = 2.0;

            Assert.AreNotEqual(oldText, _viewModel.DisplayText);
        }

        [TestMethod]
        public void AreaFormatting_UsesSquareUnits()
        {
            _viewModel.TopLeft = new Point(0, 0);
            _viewModel.BottomRight = new Point(10, 10);
            _viewModel.Units = "cm";

            Assert.IsTrue(_viewModel.DisplayText.Contains("cm²"));
        }
    }

    #endregion

    #region PolygonMeasurementViewModel Tests

    [TestClass]
    public class PolygonMeasurementViewModelTests : ViewModelTestBase
    {
        private PolygonMeasurementViewModel _viewModel = null!;
        private CommunityToolkit.Mvvm.Messaging.IMessenger _messenger = null!;

        [TestInitialize]
        public override void TestInitialize()
        {
            base.TestInitialize();
            _messenger = new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger();
            _viewModel = new PolygonMeasurementViewModel(_messenger);
        }

        [TestMethod]
        public void Constructor_InitializesWithEmptyVertices()
        {
            Assert.IsNotNull(_viewModel.Vertices);
            Assert.AreEqual(0, _viewModel.Vertices.Count);
            Assert.AreEqual(0, _viewModel.VertexCount);
            Assert.IsFalse(_viewModel.IsClosed);
            Assert.AreEqual("Polygon", _viewModel.MeasurementType);
        }

        [TestMethod]
        public void AddVertex_AddsVertexToCollection()
        {
            _viewModel.AddVertex(new Point(0, 0));

            Assert.AreEqual(1, _viewModel.Vertices.Count);
            Assert.AreEqual(new Point(0, 0), _viewModel.Vertices[0]);
        }

        [TestMethod]
        public void AddVertex_UpdatesVertexCount()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));

            Assert.AreEqual(3, _viewModel.VertexCount);
        }

        [TestMethod]
        public void CanClose_IsFalseWithLessThanThreeVertices()
        {
            Assert.IsFalse(_viewModel.CanClose);

            _viewModel.AddVertex(new Point(0, 0));
            Assert.IsFalse(_viewModel.CanClose);

            _viewModel.AddVertex(new Point(10, 0));
            Assert.IsFalse(_viewModel.CanClose);
        }

        [TestMethod]
        public void CanClose_IsTrueWithThreeOrMoreVertices()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));

            Assert.IsTrue(_viewModel.CanClose);
        }

        [TestMethod]
        public void CanClose_IsFalseWhenAlreadyClosed()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));
            _viewModel.Close();

            Assert.IsFalse(_viewModel.CanClose);
        }

        [TestMethod]
        public void Close_SetsIsClosedToTrue()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));

            _viewModel.Close();

            Assert.IsTrue(_viewModel.IsClosed);
        }

        [TestMethod]
        public void Close_DoesNothingWithLessThanThreeVertices()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));

            _viewModel.Close();

            Assert.IsFalse(_viewModel.IsClosed);
        }

        [TestMethod]
        public void UpdateVertex_ChangesVertexAtIndex()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));

            _viewModel.UpdateVertex(0, new Point(5, 5));

            Assert.AreEqual(new Point(5, 5), _viewModel.Vertices[0]);
        }

        [TestMethod]
        public void UpdateVertex_DoesNothingForInvalidIndex()
        {
            _viewModel.AddVertex(new Point(0, 0));

            _viewModel.UpdateVertex(5, new Point(10, 10));

            Assert.AreEqual(1, _viewModel.Vertices.Count);
        }

        [TestMethod]
        public void Perimeter_CalculatesCorrectlyForOpenPolygon()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));

            var expectedPerimeter = GeometryMathHelper.PolygonPerimeter(_viewModel.Vertices.ToList(), false);
            Assert.AreEqual(expectedPerimeter, _viewModel.Perimeter, 0.001);
        }

        [TestMethod]
        public void Perimeter_CalculatesCorrectlyForClosedPolygon()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));
            _viewModel.Close();

            var expectedPerimeter = GeometryMathHelper.PolygonPerimeter(_viewModel.Vertices.ToList(), true);
            Assert.AreEqual(expectedPerimeter, _viewModel.Perimeter, 0.001);
        }

        [TestMethod]
        public void Area_IsZeroForOpenPolygon()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));

            Assert.AreEqual(0, _viewModel.Area, 0.001);
        }

        [TestMethod]
        public void Area_CalculatesCorrectlyForClosedPolygon()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));
            _viewModel.Close();

            var expectedArea = GeometryMathHelper.PolygonArea(_viewModel.Vertices.ToList());
            Assert.AreEqual(expectedArea, _viewModel.Area, 0.001);
        }

        [TestMethod]
        public void DisplayText_ShowsAddMorePointsWithLessThanTwoVertices()
        {
            Assert.IsTrue(_viewModel.DisplayText.Contains("Add more points"));

            _viewModel.AddVertex(new Point(0, 0));
            Assert.IsTrue(_viewModel.DisplayText.Contains("Add more points"));
        }

        [TestMethod]
        public void DisplayText_ShowsPerimeterAndPointCountForOpenPolygon()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));

            Assert.IsTrue(_viewModel.DisplayText.Contains("P:"));
            Assert.IsTrue(_viewModel.DisplayText.Contains("3 points"));
        }

        [TestMethod]
        public void DisplayText_ShowsPerimeterAndAreaForClosedPolygon()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));
            _viewModel.Close();

            Assert.IsTrue(_viewModel.DisplayText.Contains("P:"));
            Assert.IsTrue(_viewModel.DisplayText.Contains("A:"));
        }

        [TestMethod]
        public void Perimeter_UpdatesWhenVertexIsAdded()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            var initialPerimeter = _viewModel.Perimeter;

            _viewModel.AddVertex(new Point(10, 10));

            Assert.IsTrue(_viewModel.Perimeter > initialPerimeter);
        }

        [TestMethod]
        public void Perimeter_UpdatesWhenVertexIsModified()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));
            var initialPerimeter = _viewModel.Perimeter;

            _viewModel.UpdateVertex(2, new Point(20, 20));

            Assert.AreNotEqual(initialPerimeter, _viewModel.Perimeter);
        }

        [TestMethod]
        public void Area_UpdatesWhenClosingPolygon()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));

            Assert.AreEqual(0, _viewModel.Area);

            _viewModel.Close();

            Assert.IsTrue(_viewModel.Area > 0);
        }

        [TestMethod]
        public void DisplayText_UpdatesWithScaleFactor()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(100, 0));
            _viewModel.AddVertex(new Point(100, 100));
            _viewModel.Units = "cm";
            _viewModel.ScaleFactor = 1.0;

            var oldText = _viewModel.DisplayText;
            _viewModel.ScaleFactor = 2.0;

            Assert.AreNotEqual(oldText, _viewModel.DisplayText);
        }

        [TestMethod]
        public void VertexCollectionChanged_TriggersRecalculation()
        {
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            var oldPerimeter = _viewModel.Perimeter;

            _viewModel.Vertices.Clear();
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(5, 0));

            Assert.AreNotEqual(oldPerimeter, _viewModel.Perimeter);
        }

        [TestMethod]
        public void Triangle_CalculationIsCorrect()
        {
            // Right triangle with base 10 and height 10
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(0, 10));
            _viewModel.Close();

            // Area should be (10 * 10) / 2 = 50
            var expectedArea = 50.0;
            Assert.AreEqual(expectedArea, _viewModel.Area, 0.1);
        }

        [TestMethod]
        public void Square_CalculationIsCorrect()
        {
            // Square with side 10
            _viewModel.AddVertex(new Point(0, 0));
            _viewModel.AddVertex(new Point(10, 0));
            _viewModel.AddVertex(new Point(10, 10));
            _viewModel.AddVertex(new Point(0, 10));
            _viewModel.Close();

            // Area should be 100
            Assert.AreEqual(100.0, _viewModel.Area, 0.1);

            // Perimeter should be 40
            Assert.AreEqual(40.0, _viewModel.Perimeter, 0.1);
        }
    }

    #endregion
}
