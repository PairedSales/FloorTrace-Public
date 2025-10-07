using System.Drawing;
using FloorTrace.Models;
using FloorTrace.Services;
using Moq;
using Xunit;

namespace FloorTrace.Tests
{
    public class ScaleCalculationServiceTests
    {
        private readonly Mock<IImageProcessingService> _mockImageProcessingService;
        private readonly ScaleCalculationService _scaleService;

        public ScaleCalculationServiceTests()
        {
            _mockImageProcessingService = new Mock<IImageProcessingService>();
            _scaleService = new ScaleCalculationService(_mockImageProcessingService.Object);
        }

        [Fact]
        public async Task CalculateScaleFromRoomAsync_Room1_ReturnsCorrectScale()
        {
            // Arrange: Room with 120px x 150px bounds and 12' x 15' dimensions
            var room = new Room
            {
                Bounds = new RectangleF(0, 0, 120, 150),
                WidthFeet = 12.0,
                HeightFeet = 15.0
            };

            var mockImage = new System.Windows.Media.Imaging.BitmapImage();

            // Act
            var result = await _scaleService.CalculateScaleFromRoomAsync(room, mockImage);

            // Assert: Expected scale = average of (120/12) and (150/15) = average of 10 and 10 = 10 px/ft
            Assert.Equal(10.0, result, precision: 2);
        }

        [Fact]
        public async Task CalculateScaleFromRoomAsync_Room2_ReturnsCorrectScale()
        {
            // Arrange: Room with 200px x 300px bounds and 10' x 15' dimensions
            var room = new Room
            {
                Bounds = new RectangleF(0, 0, 200, 300),
                WidthFeet = 10.0,
                HeightFeet = 15.0
            };

            var mockImage = new System.Windows.Media.Imaging.BitmapImage();

            // Act
            var result = await _scaleService.CalculateScaleFromRoomAsync(room, mockImage);

            // Assert: Expected scale = average of (200/10) and (300/15) = average of 20 and 20 = 20 px/ft
            Assert.Equal(20.0, result, precision: 2);
        }

        [Fact]
        public async Task CalculateScaleFromRoomAsync_Room3_ReturnsCorrectScale()
        {
            // Arrange: Room with 180px x 240px bounds and 15' x 20' dimensions
            var room = new Room
            {
                Bounds = new RectangleF(0, 0, 180, 240),
                WidthFeet = 15.0,
                HeightFeet = 20.0
            };

            var mockImage = new System.Windows.Media.Imaging.BitmapImage();

            // Act
            var result = await _scaleService.CalculateScaleFromRoomAsync(room, mockImage);

            // Assert: Expected scale = average of (180/15) and (240/20) = average of 12 and 12 = 12 px/ft
            Assert.Equal(12.0, result, precision: 2);
        }

        [Fact]
        public async Task CalculateScaleFromRoomAsync_InvalidDimensions_ThrowsArgumentException()
        {
            // Arrange: Room with zero dimensions
            var room = new Room
            {
                Bounds = new RectangleF(0, 0, 100, 100),
                WidthFeet = 0.0,
                HeightFeet = 15.0
            };

            var mockImage = new System.Windows.Media.Imaging.BitmapImage();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _scaleService.CalculateScaleFromRoomAsync(room, mockImage));
        }

        [Fact]
        public async Task ValidateRoomDimensionsAsync_ValidDimensions_ReturnsTrue()
        {
            // Arrange: Valid dimension strings
            var validDimensions = new[] { "12x15", "12'x15'", "12 ft x 15 ft", "12' 6\" x 15' 3\"" };

            foreach (var dimensions in validDimensions)
            {
                // Act
                var result = await _scaleService.ValidateRoomDimensionsAsync(dimensions);

                // Assert
                Assert.True(result, $"Expected '{dimensions}' to be valid");
            }
        }

        [Fact]
        public async Task ValidateRoomDimensionsAsync_InvalidDimensions_ReturnsFalse()
        {
            // Arrange: Invalid dimension strings
            var invalidDimensions = new[] { "", "12x", "x15", "12x15x20", "invalid", "12m x 15m" };

            foreach (var dimensions in invalidDimensions)
            {
                // Act
                var result = await _scaleService.ValidateRoomDimensionsAsync(dimensions);

                // Assert
                Assert.False(result, $"Expected '{dimensions}' to be invalid");
            }
        }
    }
}
