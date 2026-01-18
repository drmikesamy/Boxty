using Xunit;
using FluentAssertions;
using Moq;
using Boxty.ServerBase.Commands;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Mappers;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Boxty.ServerBase.Tests.Commands
{
    public class UpdateCommandTests
    {
        private readonly Mock<IDbContext<TestDbContext>> _mockDbContext;
        private readonly Mock<DbSet<TestEntity>> _mockDbSet;
        private readonly Mock<IMapper<TestEntity, TestDto>> _mockMapper;
        private readonly Mock<IAuthorizationService> _mockAuthService;
        private readonly Mock<IValidator<TestDto>> _mockValidator;
        private readonly UpdateCommand<TestEntity, TestDto, TestDbContext> _command;
        private readonly ClaimsPrincipal _user;

        public UpdateCommandTests()
        {
            _mockDbContext = new Mock<IDbContext<TestDbContext>>();
            _mockDbSet = new Mock<DbSet<TestEntity>>();
            _mockMapper = new Mock<IMapper<TestEntity, TestDto>>();
            _mockAuthService = new Mock<IAuthorizationService>();
            _mockValidator = new Mock<IValidator<TestDto>>();
            _user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "test-user") }));

            _mockDbContext.Setup(db => db.Set<TestEntity>()).Returns(_mockDbSet.Object);

            _command = new UpdateCommand<TestEntity, TestDto, TestDbContext>(
                _mockDbContext.Object,
                _mockMapper.Object,
                _mockAuthService.Object,
                _mockValidator.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldThrowArgumentNullException_WhenDtoIsNull()
        {
            // Arrange
            TestDto dto = null!;

            // Act
            Func<Task> act = async () => await _command.Handle(dto, _user);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("dto");
        }

        [Fact]
        public async Task Handle_ShouldThrowValidationException_WhenValidationFails()
        {
            // Arrange
            var dto = new TestDto { Id = Guid.NewGuid(), Name = "" };
            var validationFailures = new List<ValidationFailure>
            {
                new ValidationFailure("Name", "Name is required")
            };
            var validationResult = new ValidationResult(validationFailures);
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);

            // Act
            Func<Task> act = async () => await _command.Handle(dto, _user);

            // Assert
            await act.Should().ThrowAsync<ValidationException>()
                .WithMessage("*Name is required*");
        }

        [Fact]
        public async Task Handle_ShouldThrowKeyNotFoundException_WhenEntityDoesNotExist()
        {
            // Arrange
            var dto = new TestDto { Id = Guid.NewGuid(), Name = "Test" };
            var validationResult = new ValidationResult();
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);
            
            _mockDbSet.Setup(db => db.FindAsync(dto.Id))
                .ReturnsAsync((TestEntity?)null);

            // Act
            Func<Task> act = async () => await _command.Handle(dto, _user);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"Entity with ID {dto.Id} not found.");
        }

        [Fact]
        public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenAuthorizationFails()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var dto = new TestDto { Id = entityId, Name = "Test" };
            var existingEntity = new TestEntity { Id = entityId };
            var validationResult = new ValidationResult();
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);
            
            _mockDbSet.Setup(db => db.FindAsync(dto.Id))
                .ReturnsAsync(existingEntity);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, existingEntity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Failed());

            // Act
            Func<Task> act = async () => await _command.Handle(dto, _user);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("Authorization failed for resource-access policy.");
        }

        [Fact]
        public async Task Handle_ShouldUpdateEntity_WhenValidationAndAuthorizationSucceed()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var dto = new TestDto { Id = entityId, Name = "Updated Name" };
            var existingEntity = new TestEntity { Id = entityId, IsActive = true };
            var validationResult = new ValidationResult();
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);
            
            _mockDbSet.Setup(db => db.FindAsync(dto.Id))
                .ReturnsAsync(existingEntity);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, existingEntity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockDbContext.Setup(db => db.SaveChangesWithAuditAsync(_user, default))
                .ReturnsAsync(1);

            // Act
            var result = await _command.Handle(dto, _user);

            // Assert
            result.Should().Be(entityId);
            _mockMapper.Verify(m => m.Map(dto, existingEntity), Times.Once);
            _mockDbContext.Verify(db => db.SaveChangesWithAuditAsync(_user, default), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldCallValidator_WithCorrectDto()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var dto = new TestDto { Id = entityId, Name = "Test" };
            var validationResult = new ValidationResult();
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);
            
            _mockDbSet.Setup(db => db.FindAsync(dto.Id))
                .ReturnsAsync(new TestEntity { Id = entityId });
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()))
                .ReturnsAsync(AuthorizationResult.Success());

            // Act
            await _command.Handle(dto, _user);

            // Assert
            _mockValidator.Verify(v => v.ValidateAsync(dto, default), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturnEntityId_OnSuccessfulUpdate()
        {
            // Arrange
            var expectedId = Guid.NewGuid();
            var dto = new TestDto { Id = expectedId, Name = "Test" };
            var existingEntity = new TestEntity { Id = expectedId };
            var validationResult = new ValidationResult();
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);
            
            _mockDbSet.Setup(db => db.FindAsync(dto.Id))
                .ReturnsAsync(existingEntity);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, existingEntity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockDbContext.Setup(db => db.SaveChangesWithAuditAsync(_user, default))
                .ReturnsAsync(1);

            // Act
            var result = await _command.Handle(dto, _user);

            // Assert
            result.Should().Be(expectedId);
        }

        [Fact]
        public async Task Handle_ShouldMapDtoToExistingEntity_AfterValidationAndAuthorization()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var dto = new TestDto { Id = entityId, Name = "Updated" };
            var existingEntity = new TestEntity { Id = entityId };
            var validationResult = new ValidationResult();
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);
            
            _mockDbSet.Setup(db => db.FindAsync(dto.Id))
                .ReturnsAsync(existingEntity);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, existingEntity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());

            // Act
            await _command.Handle(dto, _user);

            // Assert
            _mockMapper.Verify(m => m.Map(dto, existingEntity), Times.Once);
        }

        // Test entity and DTO classes
        public class TestEntity : IEntity
        {
            public Guid Id { get; set; }
            public bool IsActive { get; set; }
            public string CreatedBy { get; set; } = string.Empty;
            public string LastModifiedBy { get; set; } = string.Empty;
            public DateTime CreatedDate { get; set; }
            public DateTime ModifiedDate { get; set; }
            public Guid SubjectId { get; set; }
            public Guid TenantId { get; set; }
            public Guid CreatedById { get; set; }
            public Guid ModifiedById { get; set; }
        }

        public class TestDto : IDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class TestDbContext : IDbContext<TestDbContext>
        {
            public DbSet<T> Set<T>() where T : class => throw new NotImplementedException();
            public int SaveChanges() => 0;
            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
            public Task<int> SaveChangesWithAuditAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default) => Task.FromResult(0);
        }
    }
}
