using System.Text.Json.Serialization;

namespace CustomerPortal.Models.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record LoginRequest(string Email, string Password);
