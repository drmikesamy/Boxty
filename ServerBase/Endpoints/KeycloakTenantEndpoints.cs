using System.Security.Claims;
using Boxty.ServerBase.Auth.Constants;
using Boxty.ServerBase.Commands;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Boxty.ServerBase.Endpoints
{
    public abstract class KeycloakTenantEndpoints<T, TDto, TContext> : BaseEndpoints<T, TDto, TContext>
    where T : class, IEntity, ITenantEntity
    where TDto : IDto, ITenant
    where TContext : IDbContext<TContext>
    {
        protected override void MapCreateEndpoint(RouteGroupBuilder group)
        {
            // Create permission for POST operations
            var createPermission = PermissionHelper.GeneratePermission<T>(PermissionOperations.Create);

            group.MapPost("/Create", (
                [FromServices] ICreateTenantCommand<T, TDto, TContext> createTenantCommand,
                ClaimsPrincipal user,
                TDto dto
            ) => Create(createTenantCommand, user, dto))
            .RequireAuthorization($"Permission:{createPermission}");
        }

        protected override void MapDeleteEndpoint(RouteGroupBuilder group)
        {
            var deletePermission = PermissionHelper.GeneratePermission<T>(PermissionOperations.Delete);

            group.MapDelete("/Delete/{id}", (
                [FromServices] IDeleteTenantCommand<T, TDto, TContext> deleteCommand,
                ClaimsPrincipal user,
                Guid id
            ) => Delete(deleteCommand, user, id))
            .RequireAuthorization($"Permission:{deletePermission}");
        }

        protected async Task<IResult> Create(
            ICreateTenantCommand<T, TDto, TContext> createTenantCommand,
            ClaimsPrincipal user,
            TDto dto)
        {
            return await ExecuteWithValidation(async () =>
            {
                var result = await createTenantCommand.Handle(dto, user);
                return Results.Ok(result);
            }, $"An error occurred while creating the {typeof(T).Name}.");
        }
        protected async Task<IResult> Delete(IDeleteTenantCommand<T, TDto, TContext> deleteCommand, ClaimsPrincipal user, Guid id)
        {
            return await Execute(async () =>
            {
                var result = await deleteCommand.Handle(id, user);
                return Results.Ok(result);
            }, $"An error occurred while deleting the {typeof(T).Name}.");
        }
    }
}