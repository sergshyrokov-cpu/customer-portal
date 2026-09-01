namespace CustomerPortal.Models.Dtos;

public record ErrorResponse(
    DateTimeOffset Timestamp,
    int Status,
    string Error,
    string Message,
    string Path,
    IReadOnlyList<FieldError>? FieldErrors = null);

public record FieldError(string Field, string Message);
