using Xunit;
using FluentAssertions;
using Moq;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;

namespace Boxty.ClientBase.Tests.Services
{
    /// <summary>
    /// Service tests for CrudService
    /// Note: Full CrudService tests require HttpClient mocking with proper configuration.
    /// This demonstrates basic test structure.
    /// </summary>
    public class CrudServiceTests
    {
        [Fact]
        public void TestDto_ShouldImplementRequiredInterfaces()
        {
            // Arrange
            var dto = new TestDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Item"
            };

            // Assert
            dto.Should().BeAssignableTo<IDto>();
            dto.Should().BeAssignableTo<IAutoCrud>();
            dto.CrudEndpoint.Should().Be("test");
            dto.DisplayName.Should().Be("Test Item");
        }

        [Fact]
        public void TestDto_DisplayName_ShouldReturnName()
        {
            // Arrange
            var dto = new TestDto
            {
                Name = "Sample Name"
            };

            // Act
            var displayName = dto.DisplayName;

            // Assert
            displayName.Should().Be("Sample Name");
        }

        // Test DTO class
        public class TestDto : IDto, IAutoCrud
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string CrudEndpoint => "test";
            public string CrudDisplayProperty => nameof(Name);
            public string DisplayName => Name;
        }
    }
}
