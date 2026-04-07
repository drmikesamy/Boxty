using System.Security.Claims;
using Boxty.ServerBase.Auth.Constants;
using Boxty.ServerBase.Commands;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Interfaces;
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
                [FromServices] IGetAllQuery<T, TDto, TContext> getAllQuery,
                ClaimsPrincipal user,
                [FromQuery] Guid? tenantId = null,
                [FromQuery] Guid? subjectId = null
            ) => GetAll(getAllQuery, user, tenantId, subjectId))
            .RequireAuthorization($"Permission:{viewPermission}");

            group.MapGet("/GetById/{id}", (
                [FromServices] IGetByIdQuery<T, TDto, TContext> getByIdQuery,
                ClaimsPrincipal user,
                Guid id,
                [FromQuery] Guid? tenantId = null,
                [FromQuery] Guid? subjectId = null
            ) => GetById(getByIdQuery, user, id, tenantId, subjectId))
            .RequireAuthorization($"Permission:{viewPermission}");

            group.MapPost("/GetByIds", (
                [FromServices] IGetByIdsQuery<T, TDto, TContext> getByIdsQuery,
                ClaimsPrincipal user,
                List<Guid> ids,
                [FromQuery] Guid? tenantId = null,
                [FromQuery] Guid? subjectId = null
            ) => GetByIds(getByIdsQuery, user, ids, tenantId, subjectId))
            .RequireAuthorization($"Permission:{viewPermission}");

            group.MapGet("/Search", (
                [FromServices] ISearchQuery<T, TDto, TContext> searchQuery,
                ClaimsPrincipal user,
                [FromQuery] string term,
                [FromQuery] Guid? tenantId = null,
                [FromQuery] Guid? subjectId = null
            ) => Search(searchQuery, user, term, tenantId, subjectId))
            .RequireAuthorization($"Permission:{viewPermission}");

            group.MapGet("/Paged", (
                [FromServices] IGetPagedQuery<T, TDto, TContext> getPagedQuery,
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
                [FromServices] ICreateCommand<T, TDto, TContext> createCommand,
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
                [FromServices] IUpdateCommand<T, TDto, TContext> updateCommand,
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
                [FromServices] IDeleteCommand<T, TContext> deleteCommand,
                [FromServices] IServiceProvider serviceProvider,
                ClaimsPrincipal user,
                Guid id
            ) => Delete(deleteCommand, user, id, serviceProvider))
            .RequireAuthorization($"Permission:{deletePermission}");
        }

        protected async Task<IResult> GetAll(IGetAllQuery<T, TDto, TContext> getAllQuery, ClaimsPrincipal user, Guid? tenantId, Guid? subjectId)
        {
            return await Execute(async () =>
            {
                var allEntities = await getAllQuery.Handle(user, tenantId, subjectId);
                return ToCollectionResult(allEntities);
            }, $"An error occurred while retrieving {typeof(T).Name} entities.");
        }

        protected async Task<IResult> GetById(IGetByIdQuery<T, TDto, TContext> getByIdQuery, ClaimsPrincipal user, Guid id, Guid? tenantId, Guid? subjectId)
        {
            return await Execute(async () =>
            {
                var entity = await getByIdQuery.Handle(id, user, tenantId, subjectId);
                return entity == null ? Results.NoContent() : Results.Ok(entity);
            }, $"An error occurred while retrieving the {typeof(T).Name} details.");
        }

        protected async Task<IResult> GetByIds(IGetByIdsQuery<T, TDto, TContext> getByIdsQuery, ClaimsPrincipal user, List<Guid> ids, Guid? tenantId, Guid? subjectId)
        {
            return await Execute(async () =>
            {
                var entities = await getByIdsQuery.Handle(ids, user, tenantId, subjectId);
                return ToCollectionResult(entities);
            }, $"An error occurred while retrieving {typeof(T).Name} entities.");
        }

        protected async Task<IResult> GetPaged(IGetPagedQuery<T, TDto, TContext> getPagedQuery, ClaimsPrincipal user, int page, int pageSize, FetchFilter? filter, Guid? tenantId, Guid? subjectId)
        {
            return await Execute(async () =>
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

                return pagedResult.Items == null || !pagedResult.Items.Any()
                    ? Results.NoContent()
                    : Results.Ok(pagedResult);
            }, $"An error occurred while retrieving {typeof(T).Name} entities.");
        }

        protected async Task<IResult> Search(ISearchQuery<T, TDto, TContext> searchQuery, ClaimsPrincipal user, string term, Guid? tenantId, Guid? subjectId)
        {
            return await Execute(async () =>
            {
                var results = await searchQuery.Handle(user, term, tenantId, subjectId);
                return ToCollectionResult(results);
            }, $"An error occurred while searching for {typeof(T).Name} entities.");
        }

        protected virtual async Task<IResult> Create(ICreateCommand<T, TDto, TContext> createCommand, ClaimsPrincipal user, TDto dto, IServiceProvider serviceProvider)
        {
            return await ExecuteWithValidation(async () =>
            {
                await OnBeforeCreate(user, dto, serviceProvider);
                var result = await createCommand.Handle(dto, user);
                await OnAfterCreate(result, user, dto, serviceProvider);
                return Results.Ok(result);
            }, $"An error occurred while creating the {typeof(T).Name}.");
        }

        protected virtual async Task<IResult> Update(IUpdateCommand<T, TDto, TContext> updateCommand, ClaimsPrincipal user, TDto dto, IServiceProvider serviceProvider)
        {
            return await ExecuteWithValidation(async () =>
            {
                await OnBeforeUpdate(user, dto, serviceProvider);
                var result = await updateCommand.Handle(dto, user);
                await OnAfterUpdate(result, user, dto, serviceProvider);
                return Results.Ok(result);
            }, $"An error occurred while updating the {typeof(T).Name}.");
        }

        protected async Task<IResult> Delete(IDeleteCommand<T, TContext> deleteCommand, ClaimsPrincipal user, Guid id, IServiceProvider serviceProvider)
        {
            return await Execute(async () =>
            {
                await OnBeforeDelete(id, user, serviceProvider);
                var result = await deleteCommand.Handle(id, user);
                await OnAfterDelete(id, user, serviceProvider);
                return Results.Ok(result);
            }, $"An error occurred while deleting the {typeof(T).Name}.");
        }

        protected Task<IResult> Execute(Func<Task<IResult>> action, string errorMessage)
        {
            return ExecuteWithHandlers(action, errorMessage);
        }

        protected Task<IResult> ExecuteWithValidation(Func<Task<IResult>> action, string errorMessage)
        {
            return ExecuteWithHandlers(action, errorMessage, HandleValidationException);
        }

        protected IResult ToCollectionResult<TItem>(IEnumerable<TItem>? items)
        {
            return items == null || !items.Any()
                ? Results.NoContent()
                : Results.Ok(items);
        }

        private async Task<IResult> ExecuteWithHandlers(
            Func<Task<IResult>> action,
            string errorMessage,
            Func<ValidationException, IResult>? validationHandler = null)
        {
            try
            {
                return await action();
            }
            catch (ValidationException validationEx) when (validationHandler != null)
            {
                return validationHandler(validationEx);
            }
            catch (Exception)
            {
                return Results.Problem(errorMessage);
            }
        }

        private static IResult HandleValidationException(ValidationException validationEx)
        {
            var errors = validationEx.Errors.Select(e => new
            {
                Property = e.PropertyName,
                Message = e.ErrorMessage
            });

            return Results.BadRequest(new
            {
                message = "Validation failed",
                errors
            });
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