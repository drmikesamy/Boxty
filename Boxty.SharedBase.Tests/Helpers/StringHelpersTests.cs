using Xunit;
using FluentAssertions;
using Boxty.SharedBase.Helpers;

namespace Boxty.SharedBase.Tests.Helpers
{
    public class StringExtensionsTests
    {
        [Fact]
        public void ContainsIgnoreCase_ShouldReturnTrue_WhenSubstringExists()
        {
            // Arrange
            string input = "Hello World";
            string substring = "hello";

            // Act
            var result = input.ContainsIgnoreCase(substring);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ContainsIgnoreCase_ShouldReturnFalse_WhenSubstringDoesNotExist()
        {
            // Arrange
            string input = "Hello World";
            string substring = "goodbye";

            // Act
            var result = input.ContainsIgnoreCase(substring);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("   ", true)]
        [InlineData("test", false)]
        public void IsNullOrWhiteSpace_ShouldReturnExpectedResult(string input, bool expected)
        {
            // Act
            var result = string.IsNullOrWhiteSpace(input);

            // Assert
            result.Should().Be(expected);
        }
    }
}
