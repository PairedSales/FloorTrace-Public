using FloorTrace.Utilities;
using Xunit;

namespace FloorTrace.Tests
{
    public class DimensionParserTests
    {
        [Theory]
        [InlineData("12x15", true)]
        [InlineData("12'x15'", true)]
        [InlineData("12 ft x 15 ft", true)]
        [InlineData("12' 6\" x 15' 3\"", true)]
        [InlineData("12×15", true)] // Unicode multiplication sign
        [InlineData("11' - 5\" x 5' - 10\"", true)]
        [InlineData("12 X 15", true)]
        [InlineData("11.5 x 9.25", true)]
        [InlineData("", false)]
        [InlineData("12x", false)]
        [InlineData("x15", false)]
        [InlineData("12x15x20", false)]
        [InlineData("invalid", false)]
        [InlineData("12m x 15m", false)]
        public void IsDimensionString_ValidatesCorrectly(string input, bool expected)
        {
            // Act
            var result = DimensionParser.IsDimensionString(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TryParseDimensionsFeet_SimpleFormat_ReturnsCorrectValues()
        {
            // Arrange: "12x15" format
            var dimensions = "12x15";
            double expectedWidth = 12.0;
            double expectedHeight = 15.0;

            // Act
            var success = DimensionParser.TryParseDimensionsFeet(dimensions, out var actualWidth, out var actualHeight);

            // Assert
            Assert.True(success, "Should successfully parse '12x15'");
            Assert.Equal(expectedWidth, actualWidth, precision: 2);
            Assert.Equal(expectedHeight, actualHeight, precision: 2);
        }

        [Fact]
        public void TryParseDimensionsFeet_FeetInchesFormat_ReturnsCorrectValues()
        {
            // Arrange: "12' 6\" x 15' 3\"" format
            var dimensions = "12' 6\" x 15' 3\"";
            double expectedWidth = 12.5;  // 12 feet + 6 inches = 12.5 feet
            double expectedHeight = 15.25; // 15 feet + 3 inches = 15.25 feet

            // Act
            var success = DimensionParser.TryParseDimensionsFeet(dimensions, out var actualWidth, out var actualHeight);

            // Assert
            Assert.True(success, "Should successfully parse feet and inches format");
            Assert.Equal(expectedWidth, actualWidth, precision: 2);
            Assert.Equal(expectedHeight, actualHeight, precision: 2);
        }

        [Fact]
        public void TryParseDimensionsFeet_DashedFeetInches_ReturnsCorrectValues()
        {
            // Arrange: "11' - 5\" x 5' - 10\"" format
            var dimensions = "11' - 5\" x 5' - 10\"";
            double expectedWidth = 11.0 + (5.0 / 12.0);
            double expectedHeight = 5.0 + (10.0 / 12.0);

            // Act
            var success = DimensionParser.TryParseDimensionsFeet(dimensions, out var actualWidth, out var actualHeight);

            // Assert
            Assert.True(success, "Should successfully parse dashed feet-inches format");
            Assert.Equal(expectedWidth, actualWidth, precision: 3);
            Assert.Equal(expectedHeight, actualHeight, precision: 3);
        }

        [Fact]
        public void TryParseDimensionsFeet_UppercaseX_ReturnsCorrectValues()
        {
            // Arrange: "12 X 15" format
            var dimensions = "12 X 15";
            double expectedWidth = 12.0;
            double expectedHeight = 15.0;

            // Act
            var success = DimensionParser.TryParseDimensionsFeet(dimensions, out var actualWidth, out var actualHeight);

            // Assert
            Assert.True(success, "Should successfully parse uppercase X separator");
            Assert.Equal(expectedWidth, actualWidth, precision: 2);
            Assert.Equal(expectedHeight, actualHeight, precision: 2);
        }

        [Fact]
        public void TryParseDimensionsFeet_DecimalFeet_ReturnsCorrectValues()
        {
            // Arrange: "11.5 x 9.25" format
            var dimensions = "11.5 x 9.25";
            double expectedWidth = 11.5;
            double expectedHeight = 9.25;

            // Act
            var success = DimensionParser.TryParseDimensionsFeet(dimensions, out var actualWidth, out var actualHeight);

            // Assert
            Assert.True(success, "Should successfully parse decimal feet");
            Assert.Equal(expectedWidth, actualWidth, precision: 2);
            Assert.Equal(expectedHeight, actualHeight, precision: 2);
        }

        [Fact]
        public void TryParseDimensionsFeet_FeetOnlyFormat_ReturnsCorrectValues()
        {
            // Arrange: "10 ft x 20 ft" format
            var dimensions = "10 ft x 20 ft";
            double expectedWidth = 10.0;
            double expectedHeight = 20.0;

            // Act
            var success = DimensionParser.TryParseDimensionsFeet(dimensions, out var actualWidth, out var actualHeight);

            // Assert
            Assert.True(success, "Should successfully parse 'ft' format");
            Assert.Equal(expectedWidth, actualWidth, precision: 2);
            Assert.Equal(expectedHeight, actualHeight, precision: 2);
        }

        [Fact]
        public void TryParseDimensionsFeet_InvalidFormat_ReturnsFalse()
        {
            // Arrange: Invalid dimension string
            var dimensions = "invalid format";

            // Act
            var success = DimensionParser.TryParseDimensionsFeet(dimensions, out var width, out var height);

            // Assert
            Assert.False(success, "Should fail to parse invalid format");
            Assert.Equal(0.0, width);
            Assert.Equal(0.0, height);
        }

        [Fact]
        public void TryParseDimensionsFeet_EmptyString_ReturnsFalse()
        {
            // Arrange: Empty string
            var dimensions = "";

            // Act
            var success = DimensionParser.TryParseDimensionsFeet(dimensions, out var width, out var height);

            // Assert
            Assert.False(success, "Should fail to parse empty string");
            Assert.Equal(0.0, width);
            Assert.Equal(0.0, height);
        }

        [Theory]
        [InlineData(0.0, "0 ft")]
        [InlineData(1.0, "1 ft")]
        [InlineData(5.5, "5' 6\"")]
        [InlineData(12.0, "12 ft")]
        [InlineData(12.5, "12' 6\"")]
        [InlineData(12.916, "12' 11\"")] // Should round to nearest inch
        public void FormatFeet_FormatsCorrectly(double feet, string expected)
        {
            // Act
            var result = DimensionParser.FormatFeet(feet);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
