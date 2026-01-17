using System.Security.Claims;
using Boxty.ServerBase.Auth.Constants;
using Boxty.ServerBase.Commands;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Mappers;
using Boxty.ServerBase.Queries;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;
using Boxty.SharedBase.Models;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Boxty.ServerBase.Endpoints
{
    public abstract class BaseEndpoints<T, TDto, TContext>
    where T : class, IEntity
    where TDto : IDto
    where TContext : IDbContext<TContext>
    {
        public virtual IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var endpointName = typeof(T).Name;
            var group = endpoints.MapGroup($"/api/{endpointName}");

            MapGetEndpoints(group);
            MapCreateEndpoint(group);
            MapUpdateEndpoint(group);
            MapDeleteEndpoint(group);

            return endpoints;
        }

        protected virtual void MapGetEndpoints(RouteGroupBuilder group)
        {
            // View permissions for GET operations (GetAll, GetById, GetByIds, Search)
            var viewPermission = PermissionHelper.GeneratePermission<T>(PermissionOperations.View);

            group.MapGet("/GetAll", (
                [FromServices] IDbContext<TContext> dbContext,
                [FromServices] IMapper<T, TDto> mapper,
                [FromServices] GetAllQuery<T, TDto, TContext> getAllQuery,
                ClaimsPrincipal user,
                [FromQuery] Guid? tenantId = null,
                [FromQuery] Guid? subjectId = null
            ) => GetAll(getAllQuery, user, tenantId, subjectId))
            .RequireAuthorization($"Permission:{viewPermission}");

            group.MapGet("/GetById/{id}", (
                [FromServices] IDbContext<TContext> dbContext,
                [FromServices] IMapper<T, TDto> mapper,
                [FromServices] GetByIdQuery<T, TDto, TContext> getByIdQuery,
                ClaimsPrincipal user,
                Guid id,
                [FromQuery] Guid? tenantId = null,
                [FromQuery] Guid? subjectId = null
            ) => GetById(getByIdQuery, user, id, tenantId, subjectId))
            .RequireAuthorization($"Permission:{viewPermission}");

            group.MapPost("/GetByIds", (
                [FromServices] IDbContext<TContext> dbContext,
                [FromServices] IMapper<T, TDto> mapper,
                [FromServices] GetByIdsQuery<T, TDto, TContext> getByIdsQuery,
                ClaimsPrincipal user,
                List<Guid> ids,
                [FromQuery] Guid? tenantId = null,
                [FromQuery] Guid? subjectId = null
            ) => GetByIds(getByIdsQuery, user, ids, tenantId, subjectId))
            .RequireAuthorization($"Permission:{viewPermission}");

            group.MapGet("/Search", (
                [FromServices] IDbContext<TContext> dbContext,
                [FromServices] IMapper<T, TDto> mapper,
                [FromServices] SearchQuery<T, TDto, TContext> searchQuery,
                ClaimsPrincipal user,
                [FromQuery] string term,
                [FromQuery] Guid? tenantId = null,
                [FromQuery] Guid? subjectId = null
            ) => Search(searchQuery, user, term, tenantId, subjectId))
            .RequireAuthorization($"Permission:{viewPermission}");

            group.MapGet("/Paged", (
                [FromServices] IDbContext<TContext> dbContext,
                [FromServices] IMapper<T, TDto> mapper,
                [FromServices] GetPagedQuery<T, TDto, TContext> getPagedQuery,
                ClaimsPrincipal user,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 10,
                [FromQuery] bool? isActive = null,
                [FromQuery] string? searchTerm = null,
                [FromQuery] DateTime? startTime = null,
                [FromQuery] DateTime? endTime = null,
                [FromQuery] Guid? tenantId = null,
                [FromQuery] Guid? subjectId = null
            ) =>
            {
                var filter = new FetchFilter
                {
                    IsActive = isActive,
                    SearchTerm = searchTerm,
                    StartTime = startTime,
                    EndTime = endTime
                };
                return GetPaged(getPagedQuery, user, page, pageSize, filter, tenantId, subjectId);
            });
        }

        protected virtual void MapCreateEndpoint(RouteGroupBuilder group)
        {
            // Create permission for POST operations
            var createPermission = PermissionHelper.GeneratePermission<T>(PermissionOperations.Create);

            group.MapPost("/Create", (
                [FromServices] IDbContext<TContext> dbContext,
                [FromServices] IMapper<T, TDto> mapper,
                [FromServices] CreateCommand<T, TDto, TContext> createCommand,
                [FromServices] IServiceProvider serviceProvider,
                ClaimsPrincipal user,
                TDto dto
            ) => Create(createCommand, user, dto, serviceProvider))
            .RequireAuthorization($"Permission:{createPermission}");
        }

        protected virtual void MapUpdateEndpoint(RouteGroupBuilder group)
        {
            // Update permission for PUT operations
            var updatePermission = PermissionHelper.GeneratePermission<T>(PermissionOperations.Update);

            group.MapPut("/Update", (
                [FromServices] IDbContext<TContext> dbContext,
                [FromServices] IMapper<T, TDto> mapper,
                [FromServices] UpdateCommand<T, TDto, TContext> updateCommand,
                [FromServices] IServiceProvider serviceProvider,
                ClaimsPrincipal user,
                TDto dto
            ) => Update(updateCommand, user, dto, serviceProvider))
            .RequireAuthorization($"Permission:{updatePermission}");
        }

        protected virtual void MapDeleteEndpoint(RouteGroupBuilder group)
        {
            // Delete permission for DELETE operations  
            var deletePermission = PermissionHelper.GeneratePermission<T>(PermissionOperations.Delete);

            group.MapDelete("/Delete/{id}", (
                [FromServices] IDbContext<TContext> dbContext,
                [FromServices] IMapper<T, TDto> mapper,
                [FromServices] DeleteCommand<T, TContext> deleteCommand,
                [FromServices] IServiceProvider serviceProvider,
                ClaimsPrincipal user,
                Guid id
            ) => Delete(deleteCommand, user, id, serviceProvider))
            .RequireAuthorization($"Permission:{deletePermission}");
        }

        protected async Task<IResult> GetAll(GetAllQuery<T, TDto, TContext> getAllQuery, ClaimsPrincipal user, Guid? tenantId, Guid? subjectId)
        {
            try
            {
                var allEntities = await getAllQuery.Handle(user, tenantId, subjectId);
                if (allEntities == null || !allEntities.Any())
                    return Results.NoContent();
                return Results.Ok(allEntities);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error occurred: {ex.Message}");
                return Results.Problem($"An error occurred while retrieving {typeof(T).Name} entities.");
            }
        }

        protected async Task<IResult> GetById(GetByIdQuery<T, TDto, TContext> getByIdQuery, ClaimsPrincipal user, Guid id, Guid? tenantId, Guid? subjectId)
        {
            try
            {
                var entity = await getByIdQuery.Handle(id, user, tenantId, subjectId);
                if (entity == null)
                    return Results.NoContent();
                return Results.Ok(entity);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error occurred: {ex.Message}");
                return Results.Problem($"An error occurred while retrieving the {typeof(T).Name} details.");
            }
        }

        protected async Task<IResult> GetByIds(GetByIdsQuery<T, TDto, TContext> getByIdsQuery, ClaimsPrincipal user, List<Guid> ids, Guid? tenantId, Guid? subjectId)
        {
            try
            {
                var entities = await getByIdsQuery.Handle(ids, user, tenantId, subjectId);
                if (entities == null || !entities.Any())
                    return Results.NoContent();
                return Results.Ok(entities);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error occurred: {ex.Message}");
                return Results.Problem($"An error occurred while retrieving the {typeof(T).Name} entities.");
            }
        }

        protected async Task<IResult> GetPaged(GetPagedQuery<T, TDto, TContext> getPagedQuery, ClaimsPrincipal user, int page, int pageSize, FetchFilter? filter, Guid? tenantId, Guid? subjectId)
        {
            try
            {
                // Only pass the filter if TDto implements IAutoCrud
                PagedResult<TDto> pagedResult;
                if (typeof(IAutoCrud).IsAssignableFrom(typeof(TDto)))
                {
                    pagedResult = await getPagedQuery.Handle(page, pageSize, user, filter, tenantId, subjectId);
                }
                else
                {
                    // For DTOs that don't implement IAutoCrud, ignore the SearchTerm filter
                    var basicFilter = filter != null ? new FetchFilter
                    {
                        IsActive = filter.IsActive,
                        StartTime = filter.StartTime,
                        EndTime = filter.EndTime
                        // Omit SearchTerm since DisplayName won't be available
                    } : null;
                    pagedResult = await getPagedQuery.Handle(page, pageSize, user, basicFilter, tenantId, subjectId);
                }

                if (pagedResult.Items == null || !pagedResult.Items.Any())
                    return Results.NoContent();
                return Results.Ok(pagedResult);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error occurred: {ex.Message}");
                return Results.Problem($"An error occurred while retrieving the {typeof(T).Name} entities.");
            }
        }

        protected async Task<IResult> Search(SearchQuery<T, TDto, TContext> searchQuery, ClaimsPrincipal user, string term, Guid? tenantId, Guid? subjectId)
        {
            try
            {
                var results = await searchQuery.Handle(user, term, tenantId, subjectId);
                if (results == null || !results.Any())
                    return Results.NoContent();
                return Results.Ok(results);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error occurred: {ex.Message}");
                return Results.Problem($"An error occurred while searching for {typeof(T).Name} entities.");
            }
        }

        protected virtual async Task<IResult> Create(CreateCommand<T, TDto, TContext> createCommand, ClaimsPrincipal user, TDto dto, IServiceProvider serviceProvider)
        {
            try
            {
                await OnBeforeCreate(user, dto, serviceProvider);
                var result = await createCommand.Handle(dto, user);
                await OnAfterCreate(result, user, dto, serviceProvider);
                return Results.Ok(result);
            }
            catch (ValidationException validationEx)
            {
                // Return validation errors as a structured response
                var errors = validationEx.Errors.Select(e => new
                {
                    Property = e.PropertyName,
                    Message = e.ErrorMessage
                });
                return Results.BadRequest(new
                {
                    message = "Validation failed",
                    errors = errors
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error occurred: {ex.Message}");
                return Results.Problem($"An error occurred while creating the {typeof(T).Name}.");
            }
        }

        protected virtual async Task<IResult> Update(UpdateCommand<T, TDto, TContext> updateCommand, ClaimsPrincipal user, TDto dto, IServiceProvider serviceProvider)
        {
            try
            {
                await OnBeforeUpdate(user, dto, serviceProvider);
                var result = await updateCommand.Handle(dto, user);
                await OnAfterUpdate(result, user, dto, serviceProvider);
                return Results.Ok(result);
            }
            catch (ValidationException validationEx)
            {
                // Return validation errors as a structured response
                var errors = validationEx.Errors.Select(e => new
                {
                    Property = e.PropertyName,
                    Message = e.ErrorMessage
                });
                return Results.BadRequest(new
                {
                    message = "Validation failed",
                    errors = errors
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error occurred: {ex.Message}");
                return Results.Problem($"An error occurred while updating the {typeof(T).Name}.");
            }
        }

        protected async Task<IResult> Delete(DeleteCommand<T, TContext> deleteCommand, ClaimsPrincipal user, Guid id, IServiceProvider serviceProvider)
        {
            try
            {
                await OnBeforeDelete(id, user, serviceProvider);
                var result = await deleteCommand.Handle(id, user);
                await OnAfterDelete(id, user, serviceProvider);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error occurred: {ex.Message}");
                return Results.Problem($"An error occurred while deleting the {typeof(T).Name}.");
            }
        }
        protected virtual Task OnBeforeCreate(ClaimsPrincipal user, TDto originalDto, IServiceProvider serviceProvider)
        {
            return Task.CompletedTask;
        }
        protected virtual Task OnBeforeUpdate(ClaimsPrincipal user, TDto originalDto, IServiceProvider serviceProvider)
        {
            return Task.CompletedTask;
        }
        protected virtual Task OnBeforeDelete(Guid deletedId, ClaimsPrincipal user, IServiceProvider serviceProvider)
        {
            return Task.CompletedTask;
        }
        protected virtual Task OnAfterCreate(Guid createdId, ClaimsPrincipal user, TDto originalDto, IServiceProvider serviceProvider)
        {
            return Task.CompletedTask;
        }
        protected virtual Task OnAfterUpdate(Guid updatedId, ClaimsPrincipal user, TDto originalDto, IServiceProvider serviceProvider)
        {
            return Task.CompletedTask;
        }
        protected virtual Task OnAfterDelete(Guid deletedId, ClaimsPrincipal user, IServiceProvider serviceProvider)
        {
            return Task.CompletedTask;
        }
    }
}