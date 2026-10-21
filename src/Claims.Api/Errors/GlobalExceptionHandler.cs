using Claims.Application.Common.Exceptions;
using Claims.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Errors;

/// <summary>
/// Maps exceptions to RFC 7807 ProblemDetails. Unknown exceptions become a generic 500;
/// only the exception type and stack trace are logged (no message or request data, so no PII).
/// The framework's own ExceptionHandlerMiddleware log is switched off in appsettings for the same reason.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            AuthenticationFailedException => new ProblemDetails
            {
                // Identical for every login failure; no detail on purpose.
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials.",
            },
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "The resource was not found.",
                Detail = exception.Message,
            },
            ConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The request conflicts with existing data.",
                Detail = exception.Message,
            },
            InvalidTransitionException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The action is not allowed in the claim's current status.",
                Detail = exception.Message,
            },
            BusinessRuleException => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "A business rule was violated.",
                Detail = exception.Message,
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
            },
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            // Type and stack trace only: exception messages can carry data values (e.g. a duplicate email).
            logger.LogError("Unhandled exception {ExceptionType}{NewLine}{StackTrace}",
                exception.GetType().FullName, Environment.NewLine, exception.StackTrace);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
