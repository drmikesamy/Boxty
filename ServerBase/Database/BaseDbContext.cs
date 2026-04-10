using System.Security.Claims;
using Boxty.ServerBase.Entities;
using Microsoft.EntityFrameworkCore;

namespace Boxty.ServerBase.Database
{
    public class BaseDbContext<TContext> : DbContext, IDbContext<TContext>
    where TContext : DbContext
    {
        private sealed record AuditContext(string FullName, Guid UserId, DateTime TimestampUtc);

        public BaseDbContext(DbContextOptions<TContext> options) : base(options) { }

        public Task<int> SaveChangesWithAuditAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            var auditContext = CreateAuditContext(user);
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .ToList();

            foreach (var entry in entries)
            {
                if (entry.Entity is IEntity entity)
                {
                    ApplyAuditFields(entity, entry.State, auditContext);
                }

                NormalizeDateTimesToUtc(entry.Entity);
            }

            return SaveChangesAsync(true, cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private static AuditContext CreateAuditContext(ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                throw new UnauthorizedAccessException("User must be authenticated to save changes.");
            }

            var fullName = user.FindFirst("name")?.Value
                ?? user.FindFirst(ClaimTypes.Name)?.Value
                ?? "Unknown User";

            Guid.TryParse(user.FindFirst("sub")?.Value, out var userId);

            return new AuditContext(fullName, userId, DateTime.UtcNow);
        }

        private static void ApplyAuditFields(IEntity entity, EntityState state, AuditContext auditContext)
        {
            if (state == EntityState.Added)
            {
                entity.CreatedBy = auditContext.FullName;
                entity.CreatedDate = auditContext.TimestampUtc;
                entity.ModifiedDate = auditContext.TimestampUtc;
                entity.CreatedById = auditContext.UserId;
                entity.ModifiedById = auditContext.UserId;
                return;
            }

            if (state == EntityState.Modified)
            {
                entity.LastModifiedBy = auditContext.FullName;
                entity.ModifiedDate = auditContext.TimestampUtc;
                entity.ModifiedById = auditContext.UserId;
            }
        }

        private static void NormalizeDateTimesToUtc(object entity)
        {
            var properties = entity.GetType()
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(property => property.CanRead && property.CanWrite &&
                    (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?)));

            foreach (var property in properties)
            {
                if (property.GetValue(entity) is DateTime dateTime && dateTime != default && dateTime.Kind != DateTimeKind.Utc)
                {
                    property.SetValue(entity, DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
                }
            }
        }

    }
}
