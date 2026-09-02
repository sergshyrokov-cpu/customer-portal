namespace CustomerPortal.Exceptions;

/// <summary>
/// Thrown for every login failure (unknown email, wrong password, disabled
/// account) with no public message-accepting constructor -- the fixed
/// message is a compile-time guarantee that FR-7/FR-16's byte-for-byte
/// uniform response can never diverge by call site (implementation-plan v2
/// Architectural Changes item 1).
/// </summary>
public sealed class AuthenticationFailedException() : Exception("Invalid email or password.");
