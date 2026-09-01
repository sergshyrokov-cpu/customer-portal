using CustomerPortal.Models.Dtos;
using Microsoft.AspNetCore.Diagnostics;

namespace CustomerPortal.Exceptions;

/// <summary>
/// Single place that maps exceptions to HTTP responses (architecture.md AD-6).
/// Extend the switch below with domain exception types as Stories introduce them.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, error) = exception switch
        {
            DuplicateEmailException => (StatusCodes.Status409Conflict, "Conflict"),
            InvalidPasswordException => (StatusCodes.Status400BadRequest, "Bad Request"),
            UnsupportedMediaTypeException => (StatusCodes.Status415UnsupportedMediaType, "Unsupported Media Type"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error"),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
        }

        httpContext.Response.StatusCode = status;

        var response = new ErrorResponse(
            Timestamp: DateTimeOffset.UtcNow,
            Status: status,
            Error: error,
            Message: status == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : exception.Message,
            Path: httpContext.Request.Path);

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
