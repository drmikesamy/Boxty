using System.Security.Claims;
using Boxty.ServerBase.Commands;
using Boxty.ServerBase.Endpoints;
using Boxty.ServerBase.Models.Email;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Boxty.ServerBase.Endpoints
{
    public class EmailEndpoints : IEndpoints
    {
        public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var emailGroup = endpoints.MapGroup("/api/email")
                .WithTags("Email")
                .RequireAuthorization();

            emailGroup.MapPost("/send", SendEmailAsync)
                .WithName("SendEmail")
                .WithSummary("Send an email using IONOS SMTP")
                .WithDescription("Sends an email with the specified content to the recipient address")
                .Produces<EmailSendResponse>(StatusCodes.Status200OK)
                .ProducesValidationProblem()
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status500InternalServerError);

            emailGroup.MapGet("/status/{operationId}", GetEmailStatusAsync)
                .WithName("GetEmailStatus")
                .WithSummary("Get the status of a sent email")
                .WithDescription("Retrieves the current status of an email send operation")
                .Produces<string>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status500InternalServerError);

            return endpoints;
        }

        private static async Task<IResult> SendEmailAsync(
            SendEmailRequest emailDto,
            ISendEmailCommand command,
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await command.Handle(emailDto, user, cancellationToken);

                return Results.Ok(new
                {
                    Success = result.Success,
                    OperationId = result.OperationId,
                    Status = result.Status.ToString(),
                    Message = result.Message
                });
            }
            catch (ValidationException ex)
            {
                return Results.ValidationProblem(ex.Errors.ToDictionary(
                    error => error.PropertyName,
                    error => new[] { error.ErrorMessage }
                ));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: ex.Message
                );
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Email Send Failed",
                    detail: ex.Message
                );
            }
            catch (Exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An unexpected error occurred while sending the email"
                );
            }
        }

        private static async Task<IResult> GetEmailStatusAsync(
            string operationId,
            ISendEmailCommand command,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var status = await command.GetEmailStatusAsync(operationId, cancellationToken);

                return Results.Ok(new
                {
                    OperationId = operationId,
                    Status = status.ToString(),
                    Message = $"Email status retrieved successfully: {status}"
                });
            }
            catch (ArgumentNullException ex)
            {
                return Results.BadRequest(new
                {
                    Error = "Invalid Request",
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Status Retrieval Failed",
                    detail: ex.Message
                );
            }
            catch (Exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An unexpected error occurred while retrieving email status"
                );
            }
        }
    }
}
