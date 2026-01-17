using System.Security.Claims;
using Boxty.ServerBase.Auth.Constants;
using Boxty.ServerBase.Commands;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Mappers;
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
                [FromServices] ResetPasswordCommand<T, TDto, TContext> resetPasswordCommand,
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
                [FromServices] IDbContext<TContext> dbContext,
                [FromServices] IMapper<T, TDto> mapper,
                [FromServices] CreateCommand<T, TDto, TContext> createCommand,
                [FromServices] CreateSubjectCommand<T, TDto, TContext> createSubjectCommand,
                ClaimsPrincipal user,
                TDto dto
            ) => Create(createSubjectCommand, user, dto))
            .RequireAuthorization($"Permission:{createPermission}");
        }

        protected override void MapDeleteEndpoint(RouteGroupBuilder group)
        {
            var deletePermission = PermissionHelper.GeneratePermission<T>(PermissionOperations.Delete);

            group.MapDelete("/Delete/{id}", (
                [FromServices] IDbContext<TContext> dbContext,
                [FromServices] IMapper<T, TDto> mapper,
                [FromServices] DeleteSubjectCommand<T, TDto, TContext> deleteCommand,
                ClaimsPrincipal user,
                Guid id
            ) => Delete(deleteCommand, user, id))
            .RequireAuthorization($"Permission:{deletePermission}");
        }

        protected async Task<IResult> Create(
            CreateSubjectCommand<T, TDto, TContext> createSubjectCommand,
            ClaimsPrincipal user,
            TDto dto)
        {
            try
            {
                var result = await createSubjectCommand.Handle(dto, user);
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

        protected async Task<IResult> Delete(DeleteSubjectCommand<T, TDto, TContext> deleteCommand, ClaimsPrincipal user, Guid id)
        {
            try
            {
                var result = await deleteCommand.Handle(id, user);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error occurred: {ex.Message}");
                return Results.Problem($"An error occurred while deleting the {typeof(T).Name}.");
            }
        }

        protected async Task<IResult> ResetPassword(
            ResetPasswordCommand<T, TDto, TContext> resetPasswordCommand,
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
                Console.Error.WriteLine($"Validation error: {string.Join(", ", ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))}");
                return Results.BadRequest(new { Message = "Validation failed", Errors = errors });
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Business logic error: {ex.Message}");
                return Results.BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine($"Unauthorized access error: {ex.Message}");
                return Results.Forbid();
            }
            catch (ArgumentNullException ex)
            {
                Console.Error.WriteLine($"Null argument error: {ex.Message}");
                return Results.BadRequest(new { Message = $"Required field missing: {ex.ParamName}" });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error occurred while resetting password: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return Results.Problem("An unexpected error occurred while resetting the password. Please try again.");
            }
        }
    }
}
