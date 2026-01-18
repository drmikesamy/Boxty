using Xunit;
using FluentAssertions;
using Moq;
using Boxty.ServerBase.Commands;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Boxty.ServerBase.Tests.Commands
{
    public class DeleteCommandTests
    {
        private readonly Mock<IDbContext<TestDbContext>> _mockDbContext;
        private readonly Mock<DbSet<TestEntity>> _mockDbSet;
        private readonly Mock<IAuthorizationService> _mockAuthService;
        private readonly DeleteCommand<TestEntity, TestDbContext> _command;
        private readonly ClaimsPrincipal _user;

        public DeleteCommandTests()
        {
            _mockDbContext = new Mock<IDbContext<TestDbContext>>();
            _mockDbSet = new Mock<DbSet<TestEntity>>();
            _mockAuthService = new Mock<IAuthorizationService>();
            _user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "test-user") }));

            _mockDbContext.Setup(db => db.Set<TestEntity>()).Returns(_mockDbSet.Object);

            _command = new DeleteCommand<TestEntity, TestDbContext>(
                _mockDbContext.Object,
                _mockAuthService.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturnFalse_WhenEntityDoesNotExist()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            
            _mockDbSet.Setup(db => db.FindAsync(entityId))
                .ReturnsAsync((TestEntity?)null);

            // Act
            var result = await _command.Handle(entityId, _user);

            // Assert
            result.Should().BeFalse();
            _mockDbSet.Verify(db => db.Remove(It.IsAny<TestEntity>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenAuthorizationFails()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var entity = new TestEntity { Id = entityId };
            
            _mockDbSet.Setup(db => db.FindAsync(entityId))
                .ReturnsAsync(entity);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, entity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Failed());

            // Act
            Func<Task> act = async () => await _command.Handle(entityId, _user);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("Authorization failed for resource-access policy.");
            
            _mockDbSet.Verify(db => db.Remove(It.IsAny<TestEntity>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldDeleteEntity_WhenAuthorizationSucceeds()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var entity = new TestEntity { Id = entityId };
            
            _mockDbSet.Setup(db => db.FindAsync(entityId))
                .ReturnsAsync(entity);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, entity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockDbContext.Setup(db => db.SaveChangesWithAuditAsync(_user, default))
                .ReturnsAsync(1);

            // Act
            var result = await _command.Handle(entityId, _user);

            // Assert
            result.Should().BeTrue();
            _mockDbSet.Verify(db => db.Remove(entity), Times.Once);
            _mockDbContext.Verify(db => db.SaveChangesWithAuditAsync(_user, default), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturnTrue_OnSuccessfulDeletion()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var entity = new TestEntity { Id = entityId };
            
            _mockDbSet.Setup(db => db.FindAsync(entityId))
                .ReturnsAsync(entity);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, entity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockDbContext.Setup(db => db.SaveChangesWithAuditAsync(_user, default))
                .ReturnsAsync(1);

            // Act
            var result = await _command.Handle(entityId, _user);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_ShouldFindEntityById_BeforeDeleting()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var entity = new TestEntity { Id = entityId };
            
            _mockDbSet.Setup(db => db.FindAsync(entityId))
                .ReturnsAsync(entity);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, entity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());

            // Act
            await _command.Handle(entityId, _user);

            // Assert
            _mockDbSet.Verify(db => db.FindAsync(entityId), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldCheckAuthorization_BeforeDeleting()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var entity = new TestEntity { Id = entityId };
            
            _mockDbSet.Setup(db => db.FindAsync(entityId))
                .ReturnsAsync(entity);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, entity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());

            // Act
            await _command.Handle(entityId, _user);

            // Assert
            _mockAuthService.Verify(
                a => a.AuthorizeAsync(_user, entity, "resource-access"),
                Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldNotSaveChanges_WhenEntityNotFound()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            
            _mockDbSet.Setup(db => db.FindAsync(entityId))
                .ReturnsAsync((TestEntity?)null);

            // Act
            await _command.Handle(entityId, _user);

            // Assert
            _mockDbContext.Verify(
                db => db.SaveChangesWithAuditAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Theory]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        [InlineData("11111111-1111-1111-1111-111111111111")]
        public async Task Handle_ShouldHandleDifferentGuids(string guidString)
        {
            // Arrange
            var entityId = Guid.Parse(guidString);
            
            _mockDbSet.Setup(db => db.FindAsync(entityId))
                .ReturnsAsync((TestEntity?)null);

            // Act
            var result = await _command.Handle(entityId, _user);

            // Assert
            result.Should().BeFalse();
        }

        // Test entity class
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

        public class TestDbContext : IDbContext<TestDbContext>
        {
            public DbSet<T> Set<T>() where T : class => throw new NotImplementedException();
            public int SaveChanges() => 0;
            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
            public Task<int> SaveChangesWithAuditAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default) => Task.FromResult(0);
        }
    }
}
