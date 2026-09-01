using System.Text.Json.Serialization;

namespace CustomerPortal.Models.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RegistrationRequest(string Email, string Password);
