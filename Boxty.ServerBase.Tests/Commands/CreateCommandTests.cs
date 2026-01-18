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
    public class CreateCommandTests
    {
        private readonly Mock<IDbContext<TestDbContext>> _mockDbContext;
        private readonly Mock<DbSet<TestEntity>> _mockDbSet;
        private readonly Mock<IMapper<TestEntity, TestDto>> _mockMapper;
        private readonly Mock<IAuthorizationService> _mockAuthService;
        private readonly Mock<IValidator<TestDto>> _mockValidator;
        private readonly CreateCommand<TestEntity, TestDto, TestDbContext> _command;
        private readonly ClaimsPrincipal _user;

        public CreateCommandTests()
        {
            _mockDbContext = new Mock<IDbContext<TestDbContext>>();
            _mockDbSet = new Mock<DbSet<TestEntity>>();
            _mockMapper = new Mock<IMapper<TestEntity, TestDto>>();
            _mockAuthService = new Mock<IAuthorizationService>();
            _mockValidator = new Mock<IValidator<TestDto>>();
            _user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "test-user") }));

            _mockDbContext.Setup(db => db.Set<TestEntity>()).Returns(_mockDbSet.Object);

            _command = new CreateCommand<TestEntity, TestDto, TestDbContext>(
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
            var dto = new TestDto { Id = Guid.Empty, Name = "" };
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
            
            _mockDbSet.Verify(db => db.Add(It.IsAny<TestEntity>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldCreateEntity_WhenValidationSucceeds()
        {
            // Arrange
            var dto = new TestDto { Id = Guid.Empty, Name = "Test Entity" };
            var newEntity = new TestEntity { Id = Guid.NewGuid(), IsActive = true };
            var validationResult = new ValidationResult();
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);
            
            _mockMapper.Setup(m => m.Map(dto))
                .Returns(newEntity);
            
            _mockDbContext.Setup(db => db.SaveChangesWithAuditAsync(_user, default))
                .ReturnsAsync(1);

            // Act
            var result = await _command.Handle(dto, _user, skipAuth: true);

            // Assert
            result.Should().Be(newEntity.Id);
            _mockDbSet.Verify(db => db.Add(newEntity), Times.Once);
            _mockDbContext.Verify(db => db.SaveChangesWithAuditAsync(_user, default), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldCallValidator_WithCorrectDto()
        {
            // Arrange
            var dto = new TestDto { Id = Guid.Empty, Name = "Test" };
            var validationResult = new ValidationResult();
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);
            
            _mockMapper.Setup(m => m.Map(dto))
                .Returns(new TestEntity { Id = Guid.NewGuid() });

            // Act
            try
            {
                await _command.Handle(dto, _user, skipAuth: true);
            }
            catch { }

            // Assert
            _mockValidator.Verify(v => v.ValidateAsync(dto, default), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldMapDtoToEntity_AfterValidation()
        {
            // Arrange
            var dto = new TestDto { Id = Guid.Empty, Name = "Test" };
            var entity = new TestEntity { Id = Guid.NewGuid() };
            var validationResult = new ValidationResult();
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);
            
            _mockMapper.Setup(m => m.Map(dto))
                .Returns(entity);

            // Act
            await _command.Handle(dto, _user, skipAuth: true);

            // Assert
            _mockMapper.Verify(m => m.Map(dto), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturnNewEntityId_OnSuccess()
        {
            // Arrange
            var expectedId = Guid.NewGuid();
            var dto = new TestDto { Id = Guid.Empty, Name = "Test" };
            var entity = new TestEntity { Id = expectedId };
            var validationResult = new ValidationResult();
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);
            
            _mockMapper.Setup(m => m.Map(dto))
                .Returns(entity);
            
            _mockDbContext.Setup(db => db.SaveChangesWithAuditAsync(_user, default))
                .ReturnsAsync(1);

            // Act
            var result = await _command.Handle(dto, _user, skipAuth: true);

            // Assert
            result.Should().Be(expectedId);
        }

        [Fact]
        public async Task Handle_ShouldSkipAuthorization_WhenSkipAuthIsTrue()
        {
            // Arrange
            var dto = new TestDto { Id = Guid.Empty, Name = "Test" };
            var validationResult = new ValidationResult();
            
            _mockValidator.Setup(v => v.ValidateAsync(dto, default))
                .ReturnsAsync(validationResult);
            
            _mockMapper.Setup(m => m.Map(dto))
                .Returns(new TestEntity { Id = Guid.NewGuid() });

            // Act
            await _command.Handle(dto, _user, skipAuth: true);

            // Assert
            _mockAuthService.Verify(
                a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()),
                Times.Never);
        }

        // Test entity and DTO classes for testing
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
            public DbSet<T> Set<T>() where T : class
            {
                throw new NotImplementedException();
            }

            public int SaveChanges()
            {
                return 0;
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0);
            }

            public Task<int> SaveChangesWithAuditAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0);
            }
        }
    }
}
