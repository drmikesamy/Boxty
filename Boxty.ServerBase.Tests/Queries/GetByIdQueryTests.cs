using Xunit;
using FluentAssertions;
using Moq;
using Boxty.ServerBase.Queries;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Mappers;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq.Expressions;
using MockQueryable.Moq;

namespace Boxty.ServerBase.Tests.Queries
{
    public class GetByIdQueryTests
    {
        private readonly Mock<IDbContext<TestDbContext>> _mockDbContext;
        private readonly Mock<DbSet<TestEntity>> _mockDbSet;
        private readonly Mock<IMapper<TestEntity, TestDto>> _mockMapper;
        private readonly Mock<IAuthorizationService> _mockAuthService;
        private readonly GetByIdQuery<TestEntity, TestDto, TestDbContext> _query;
        private readonly ClaimsPrincipal _user;

        public GetByIdQueryTests()
        {
            _mockDbContext = new Mock<IDbContext<TestDbContext>>();
            _mockDbSet = new Mock<DbSet<TestEntity>>();
            _mockMapper = new Mock<IMapper<TestEntity, TestDto>>();
            _mockAuthService = new Mock<IAuthorizationService>();
            _user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "test-user") }));

            _query = new GetByIdQuery<TestEntity, TestDto, TestDbContext>(
                _mockDbContext.Object,
                _mockMapper.Object,
                _mockAuthService.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturnNull_WhenEntityDoesNotExist()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var entities = new List<TestEntity>().AsQueryable();
            
            SetupMockDbSet(entities);

            // Act
            var result = await _query.Handle(entityId, _user);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenAuthorizationFails()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var entity = new TestEntity { Id = entityId };
            var entities = new List<TestEntity> { entity }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, entity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Failed());

            // Act
            Func<Task> act = async () => await _query.Handle(entityId, _user);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("Authorization failed for resource-access policy.");
        }

        [Fact]
        public async Task Handle_ShouldReturnDto_WhenEntityExistsAndAuthorizationSucceeds()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var entity = new TestEntity { Id = entityId, Name = "Test Entity" };
            var dto = new TestDto { Id = entityId, Name = "Test Entity" };
            var entities = new List<TestEntity> { entity }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, entity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(entity, _user))
                .Returns(dto);

            // Act
            var result = await _query.Handle(entityId, _user);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(entityId);
            result.Name.Should().Be("Test Entity");
        }

        [Fact]
        public async Task Handle_ShouldFilterByTenantId_WhenProvided()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var entity = new TestEntity { Id = entityId, TenantId = tenantId };
            var wrongTenantEntity = new TestEntity { Id = entityId, TenantId = Guid.NewGuid() };
            var entities = new List<TestEntity> { entity }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, It.IsAny<TestEntity>(), "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(It.IsAny<TestEntity>(), _user))
                .Returns(new TestDto { Id = entityId });

            // Act
            var result = await _query.Handle(entityId, _user, tenantId: tenantId);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task Handle_ShouldFilterBySubjectId_WhenProvided()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var entity = new TestEntity { Id = entityId, SubjectId = subjectId };
            var entities = new List<TestEntity> { entity }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, It.IsAny<TestEntity>(), "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(It.IsAny<TestEntity>(), _user))
                .Returns(new TestDto { Id = entityId });

            // Act
            var result = await _query.Handle(entityId, _user, subjectId: subjectId);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task Handle_ShouldMapEntityToDto_AfterAuthorization()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var entity = new TestEntity { Id = entityId };
            var entities = new List<TestEntity> { entity }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, entity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(entity, _user))
                .Returns(new TestDto { Id = entityId });

            // Act
            await _query.Handle(entityId, _user);

            // Assert
            _mockMapper.Verify(m => m.Map(entity, _user), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldCheckAuthorization_BeforeReturningDto()
        {
            // Arrange
            var entityId = Guid.NewGuid();
            var entity = new TestEntity { Id = entityId };
            var entities = new List<TestEntity> { entity }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, entity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(entity, _user))
                .Returns(new TestDto { Id = entityId });

            // Act
            await _query.Handle(entityId, _user);

            // Assert
            _mockAuthService.Verify(
                a => a.AuthorizeAsync(_user, entity, "resource-access"),
                Times.Once);
        }

        [Theory]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        [InlineData("11111111-1111-1111-1111-111111111111")]
        [InlineData("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
        public async Task Handle_ShouldHandleDifferentGuids(string guidString)
        {
            // Arrange
            var entityId = Guid.Parse(guidString);
            var entities = new List<TestEntity>().AsQueryable();
            
            SetupMockDbSet(entities);

            // Act
            var result = await _query.Handle(entityId, _user);

            // Assert
            result.Should().BeNull();
        }

        private void SetupMockDbSet(IQueryable<TestEntity> entities)
        {
            var mock = entities.ToList().BuildMockDbSet();
            _mockDbContext.Setup(db => db.Set<TestEntity>()).Returns(mock.Object);
        }

        // Test entity and DTO classes
        public class TestEntity : IEntity
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
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
