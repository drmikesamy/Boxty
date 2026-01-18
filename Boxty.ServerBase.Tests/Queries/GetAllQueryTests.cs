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
using MockQueryable.Moq;

namespace Boxty.ServerBase.Tests.Queries
{
    public class GetAllQueryTests
    {
        private readonly Mock<IDbContext<TestDbContext>> _mockDbContext;
        private readonly Mock<DbSet<TestEntity>> _mockDbSet;
        private readonly Mock<IMapper<TestEntity, TestDto>> _mockMapper;
        private readonly Mock<IAuthorizationService> _mockAuthService;
        private readonly GetAllQuery<TestEntity, TestDto, TestDbContext> _query;
        private readonly ClaimsPrincipal _user;

        public GetAllQueryTests()
        {
            _mockDbContext = new Mock<IDbContext<TestDbContext>>();
            _mockDbSet = new Mock<DbSet<TestEntity>>();
            _mockMapper = new Mock<IMapper<TestEntity, TestDto>>();
            _mockAuthService = new Mock<IAuthorizationService>();
            _user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "test-user") }));

            _query = new GetAllQuery<TestEntity, TestDto, TestDbContext>(
                _mockDbContext.Object,
                _mockMapper.Object,
                _mockAuthService.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoEntitiesExist()
        {
            // Arrange
            var entities = new List<TestEntity>().AsQueryable();
            SetupMockDbSet(entities);

            // Act
            var result = await _query.Handle(_user);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ShouldReturnAllAuthorizedEntities_WhenEntitiesExist()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Id = Guid.NewGuid(), Name = "Entity 1" },
                new TestEntity { Id = Guid.NewGuid(), Name = "Entity 2" },
                new TestEntity { Id = Guid.NewGuid(), Name = "Entity 3" }
            }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, It.IsAny<TestEntity>(), "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(It.IsAny<TestEntity>(), _user))
                .Returns<TestEntity, ClaimsPrincipal>((e, u) => new TestDto { Id = e.Id, Name = e.Name });

            // Act
            var result = await _query.Handle(_user);

            // Assert
            result.Should().HaveCount(3);
            result.Select(r => r.Name).Should().Contain(new[] { "Entity 1", "Entity 2", "Entity 3" });
        }

        [Fact]
        public async Task Handle_ShouldFilterOutUnauthorizedEntities()
        {
            // Arrange
            var authorizedEntity = new TestEntity { Id = Guid.NewGuid(), Name = "Authorized" };
            var unauthorizedEntity = new TestEntity { Id = Guid.NewGuid(), Name = "Unauthorized" };
            var entities = new List<TestEntity> { authorizedEntity, unauthorizedEntity }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, authorizedEntity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, unauthorizedEntity, "resource-access"))
                .ReturnsAsync(AuthorizationResult.Failed());
            
            _mockMapper.Setup(m => m.Map(It.IsAny<TestEntity>(), _user))
                .Returns<TestEntity, ClaimsPrincipal>((e, u) => new TestDto { Id = e.Id, Name = e.Name });

            // Act
            var result = await _query.Handle(_user);

            // Assert
            result.Should().ContainSingle();
            result.First().Name.Should().Be("Authorized");
        }

        [Fact]
        public async Task Handle_ShouldFilterByTenantId_WhenProvided()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var matchingEntity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Matching" };
            var nonMatchingEntity = new TestEntity { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "Non-Matching" };
            var entities = new List<TestEntity> { matchingEntity }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, It.IsAny<TestEntity>(), "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(It.IsAny<TestEntity>(), _user))
                .Returns<TestEntity, ClaimsPrincipal>((e, u) => new TestDto { Id = e.Id, Name = e.Name });

            // Act
            var result = await _query.Handle(_user, tenantId: tenantId);

            // Assert
            result.Should().ContainSingle();
            result.First().Name.Should().Be("Matching");
        }

        [Fact]
        public async Task Handle_ShouldFilterBySubjectId_WhenProvided()
        {
            // Arrange
            var subjectId = Guid.NewGuid();
            var matchingEntity = new TestEntity { Id = Guid.NewGuid(), SubjectId = subjectId, Name = "Matching" };
            var entities = new List<TestEntity> { matchingEntity }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, It.IsAny<TestEntity>(), "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(It.IsAny<TestEntity>(), _user))
                .Returns<TestEntity, ClaimsPrincipal>((e, u) => new TestDto { Id = e.Id, Name = e.Name });

            // Act
            var result = await _query.Handle(_user, subjectId: subjectId);

            // Assert
            result.Should().ContainSingle();
            result.First().Name.Should().Be("Matching");
        }

        [Fact]
        public async Task Handle_ShouldFilterByBothTenantAndSubjectId_WhenBothProvided()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var matchingEntity = new TestEntity 
            { 
                Id = Guid.NewGuid(), 
                TenantId = tenantId, 
                SubjectId = subjectId, 
                Name = "Matching Both" 
            };
            var entities = new List<TestEntity> { matchingEntity }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, It.IsAny<TestEntity>(), "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(It.IsAny<TestEntity>(), _user))
                .Returns<TestEntity, ClaimsPrincipal>((e, u) => new TestDto { Id = e.Id, Name = e.Name });

            // Act
            var result = await _query.Handle(_user, tenantId: tenantId, subjectId: subjectId);

            // Assert
            result.Should().ContainSingle();
            result.First().Name.Should().Be("Matching Both");
        }

        [Fact]
        public async Task Handle_ShouldMapEachAuthorizedEntity()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Id = Guid.NewGuid() },
                new TestEntity { Id = Guid.NewGuid() }
            }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, It.IsAny<TestEntity>(), "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(It.IsAny<TestEntity>(), _user))
                .Returns<TestEntity, ClaimsPrincipal>((e, u) => new TestDto { Id = e.Id });

            // Act
            await _query.Handle(_user);

            // Assert
            _mockMapper.Verify(m => m.Map(It.IsAny<TestEntity>(), _user), Times.Exactly(2));
        }

        [Fact]
        public async Task Handle_ShouldCheckAuthorizationForEachEntity()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Id = Guid.NewGuid() },
                new TestEntity { Id = Guid.NewGuid() },
                new TestEntity { Id = Guid.NewGuid() }
            }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, It.IsAny<TestEntity>(), "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(It.IsAny<TestEntity>(), _user))
                .Returns<TestEntity, ClaimsPrincipal>((e, u) => new TestDto { Id = e.Id });

            // Act
            await _query.Handle(_user);

            // Assert
            _mockAuthService.Verify(
                a => a.AuthorizeAsync(_user, It.IsAny<TestEntity>(), "resource-access"),
                Times.Exactly(3));
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenAllEntitiesAreUnauthorized()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Id = Guid.NewGuid() },
                new TestEntity { Id = Guid.NewGuid() }
            }.AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, It.IsAny<TestEntity>(), "resource-access"))
                .ReturnsAsync(AuthorizationResult.Failed());

            // Act
            var result = await _query.Handle(_user);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ShouldHandleLargeNumberOfEntities()
        {
            // Arrange
            var entities = Enumerable.Range(1, 100)
                .Select(i => new TestEntity { Id = Guid.NewGuid(), Name = $"Entity {i}" })
                .AsQueryable();
            
            SetupMockDbSet(entities);
            
            _mockAuthService.Setup(a => a.AuthorizeAsync(_user, It.IsAny<TestEntity>(), "resource-access"))
                .ReturnsAsync(AuthorizationResult.Success());
            
            _mockMapper.Setup(m => m.Map(It.IsAny<TestEntity>(), _user))
                .Returns<TestEntity, ClaimsPrincipal>((e, u) => new TestDto { Id = e.Id, Name = e.Name });

            // Act
            var result = await _query.Handle(_user);

            // Assert
            result.Should().HaveCount(100);
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
