namespace CustomerPortal.Exceptions;

/// <summary>
/// Thrown by the Service-layer password re-check (FR-6, security-conventions.md
/// SC-1). Not reachable from the normal HTTP flow, since request-layer
/// FluentValidation already rejects a policy-violating password before the
/// Controller action runs; this is defense in depth, not a primary path.
/// </summary>
public class InvalidPasswordException(string message) : Exception(message);
