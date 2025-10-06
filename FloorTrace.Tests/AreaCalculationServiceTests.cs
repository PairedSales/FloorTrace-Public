using System.Drawing;
using FloorTrace.Services;
using Xunit;

namespace FloorTrace.Tests
{
    public class AreaCalculationServiceTests
    {
        private readonly AreaCalculationService _areaService;

        public AreaCalculationServiceTests()
        {
            _areaService = new AreaCalculationService();
        }

        [Fact]
        public async Task CalculateAreaUsingGreensTheoremAsync_Square_ReturnsCorrectArea()
        {
            // Arrange: Create a 100x100 pixel square
            var perimeterPoints = new List<PointF>
            {
                new PointF(0, 0),    // Top-left
                new PointF(100, 0),  // Top-right
                new PointF(100, 100), // Bottom-right
                new PointF(0, 100)   // Bottom-left
            };

            // Act
            var result = await _areaService.CalculateAreaUsingGreensTheoremAsync(perimeterPoints);

            // Assert: Expected area = 100 * 100 = 10,000 square pixels
            Assert.Equal(10000.0, result, precision: 2);
        }

        [Fact]
        public async Task CalculateAreaUsingGreensTheoremAsync_Rectangle_ReturnsCorrectArea()
        {
            // Arrange: Create a 50x150 pixel rectangle
            var perimeterPoints = new List<PointF>
            {
                new PointF(0, 0),    // Top-left
                new PointF(150, 0),  // Top-right
                new PointF(150, 50), // Bottom-right
                new PointF(0, 50)    // Bottom-left
            };

            // Act
            var result = await _areaService.CalculateAreaUsingGreensTheoremAsync(perimeterPoints);

            // Assert: Expected area = 150 * 50 = 7,500 square pixels
            Assert.Equal(7500.0, result, precision: 2);
        }

        [Fact]
        public async Task CalculateAreaUsingGreensTheoremAsync_Triangle_ReturnsCorrectArea()
        {
            // Arrange: Create a triangle with vertices (0,0), (100,0), (50,100)
            var perimeterPoints = new List<PointF>
            {
                new PointF(0, 0),    // Bottom-left
                new PointF(100, 0),  // Bottom-right
                new PointF(50, 100)  // Top-center
            };

            // Act
            var result = await _areaService.CalculateAreaUsingGreensTheoremAsync(perimeterPoints);

            // Assert: Expected area = 0.5 * base * height = 0.5 * 100 * 100 = 5,000 square pixels
            Assert.Equal(5000.0, result, precision: 2);
        }

        [Fact]
        public async Task CalculateSideLengthsAsync_Square_ReturnsCorrectLengths()
        {
            // Arrange: Create a 100x100 pixel square
            var perimeterPoints = new List<PointF>
            {
                new PointF(0, 0),    // Top-left
                new PointF(100, 0),  // Top-right
                new PointF(100, 100), // Bottom-right
                new PointF(0, 100)   // Bottom-left
            };

            // Act
            var result = await _areaService.CalculateSideLengthsAsync(perimeterPoints);

            // Assert: All sides should be 100 pixels long
            Assert.Equal(4, result.Count);
            foreach (var length in result)
            {
                Assert.Equal(100.0, length, precision: 2);
            }
        }

        [Fact]
        public async Task CalculateSideLengthsAsync_Rectangle_ReturnsCorrectLengths()
        {
            // Arrange: Create a 50x150 pixel rectangle
            var perimeterPoints = new List<PointF>
            {
                new PointF(0, 0),    // Top-left
                new PointF(150, 0),  // Top-right
                new PointF(150, 50), // Bottom-right
                new PointF(0, 50)    // Bottom-left
            };

            // Act
            var result = await _areaService.CalculateSideLengthsAsync(perimeterPoints);

            // Assert: Two sides of 150px and two sides of 50px
            Assert.Equal(4, result.Count);
            Assert.Contains(150.0, result);
            Assert.Contains(50.0, result);
        }

        [Fact]
        public async Task CalculateSideLengthsAsync_Triangle_ReturnsCorrectLengths()
        {
            // Arrange: Create a triangle with vertices (0,0), (100,0), (50,100)
            var perimeterPoints = new List<PointF>
            {
                new PointF(0, 0),    // Bottom-left
                new PointF(100, 0),  // Bottom-right
                new PointF(50, 100)  // Top-center
            };

            // Act
            var result = await _areaService.CalculateSideLengthsAsync(perimeterPoints);

            // Assert: Should have 3 sides
            Assert.Equal(3, result.Count);
            
            // One side should be 100 (horizontal base)
            Assert.Contains(100.0, result);
            
            // Two sides should be equal (hypotenuses)
            var hypotenuseLength = Math.Sqrt(50 * 50 + 100 * 100); // ≈ 111.8
            var hypotenuseCount = result.Count(l => Math.Abs(l - hypotenuseLength) < 1);
            Assert.Equal(2, hypotenuseCount);
        }

        [Fact]
        public async Task ConvertPixelsToFeetAsync_Test1_ReturnsCorrectFeet()
        {
            // Arrange: 100 pixels at 10 pixels per foot
            var pixels = 100.0;
            var scale = 10.0;

            // Act
            var result = await _areaService.ConvertPixelsToFeetAsync(pixels, scale);

            // Assert: Expected result = 100 / 10 = 10 feet
            Assert.Equal(10.0, result, precision: 2);
        }

        [Fact]
        public async Task ConvertPixelsToFeetAsync_Test2_ReturnsCorrectFeet()
        {
            // Arrange: 250 pixels at 25 pixels per foot
            var pixels = 250.0;
            var scale = 25.0;

            // Act
            var result = await _areaService.ConvertPixelsToFeetAsync(pixels, scale);

            // Assert: Expected result = 250 / 25 = 10 feet
            Assert.Equal(10.0, result, precision: 2);
        }

        [Fact]
        public async Task ConvertFeetToPixelsAsync_Test1_ReturnsCorrectPixels()
        {
            // Arrange: 50 feet at 20 pixels per foot
            var feet = 50.0;
            var scale = 20.0;

            // Act
            var result = await _areaService.ConvertFeetToPixelsAsync(feet, scale);

            // Assert: Expected result = 50 * 20 = 1,000 pixels
            Assert.Equal(1000.0, result, precision: 2);
        }
    }
}
