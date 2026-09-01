namespace CustomerPortal.Models.Dtos;

public record CustomerResponse(long Id, string Email, string Role, DateTimeOffset CreatedAt);
