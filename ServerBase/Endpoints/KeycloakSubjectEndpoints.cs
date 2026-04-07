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
    public abstract class KeycloakSubjectEndpoints<T, TDto, TContext> : BaseEndpoints<T, TDto, TContext>
    where T : class, IEntity, ISubjectEntity
    where TDto : IDto, IAuditDto, ISubject
    where TContext : IDbContext<TContext>
    {
        public override IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints = base.MapEndpoints(endpoints);

            var endpointName = typeof(T).Name;
            var group = endpoints.MapGroup($"/api/{endpointName}");

            // Add password reset endpoint
            group.MapPut("/resetpassword/{id:guid}", async (
                [FromServices] IResetPasswordCommand<T, TDto, TContext> resetPasswordCommand,
                ClaimsPrincipal user,
                Guid id
            ) => await ResetPassword(resetPasswordCommand, user, id))
                .RequireAuthorization($"Permission:Create{typeof(T).Name}");

            return endpoints;
        }

        protected override void MapCreateEndpoint(RouteGroupBuilder group)
        {
            var createPermission = PermissionHelper.GeneratePermission<T>(PermissionOperations.Create);

            group.MapPost("/Create", (
                [FromServices] ICreateSubjectCommand<T, TDto, TContext> createSubjectCommand,
                ClaimsPrincipal user,
                TDto dto
            ) => Create(createSubjectCommand, user, dto))
            .RequireAuthorization($"Permission:{createPermission}");
        }

        protected override void MapDeleteEndpoint(RouteGroupBuilder group)
        {
            var deletePermission = PermissionHelper.GeneratePermission<T>(PermissionOperations.Delete);

            group.MapDelete("/Delete/{id}", (
                [FromServices] IDeleteSubjectCommand<T, TDto, TContext> deleteCommand,
                ClaimsPrincipal user,
                Guid id
            ) => Delete(deleteCommand, user, id))
            .RequireAuthorization($"Permission:{deletePermission}");
        }

        protected async Task<IResult> Create(
            ICreateSubjectCommand<T, TDto, TContext> createSubjectCommand,
            ClaimsPrincipal user,
            TDto dto)
        {
            return await ExecuteWithValidation(async () =>
            {
                var result = await createSubjectCommand.Handle(dto, user);
                return Results.Ok(result);
            }, $"An error occurred while creating the {typeof(T).Name}.");
        }

        protected async Task<IResult> Delete(IDeleteSubjectCommand<T, TDto, TContext> deleteCommand, ClaimsPrincipal user, Guid id)
        {
            return await Execute(async () =>
            {
                var result = await deleteCommand.Handle(id, user);
                return Results.Ok(result);
            }, $"An error occurred while deleting the {typeof(T).Name}.");
        }

        protected async Task<IResult> ResetPassword(
            IResetPasswordCommand<T, TDto, TContext> resetPasswordCommand,
            ClaimsPrincipal user,
            Guid id)
        {
            try
            {
                var result = await resetPasswordCommand.Handle(id, user);
                return Results.Ok(result);
            }
            catch (ValidationException ex)
            {
                var errors = ex.Errors.Select(e => new { Field = e.PropertyName, Message = e.ErrorMessage });
                return Results.BadRequest(new { Message = "Validation failed", Errors = errors });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (ArgumentNullException ex)
            {
                return Results.BadRequest(new { Message = $"Required field missing: {ex.ParamName}" });
            }
            catch (Exception)
            {
                return Results.Problem("An unexpected error occurred while resetting the password. Please try again.");
            }
        }
    }
}
