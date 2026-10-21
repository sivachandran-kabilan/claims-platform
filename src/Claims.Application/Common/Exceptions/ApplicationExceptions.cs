namespace Claims.Application.Common.Exceptions;

/// <summary>The request conflicts with existing data, e.g. duplicate email (HTTP 409).</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>The resource does not exist or the caller may not know it exists (HTTP 404).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>The request is well-formed but cannot be processed, e.g. an Idempotency-Key reused with a different body (HTTP 422).</summary>
public sealed class UnprocessableException(string message) : Exception(message);

/// <summary>
/// Login failed. Deliberately carries no detail: unknown email, wrong password and lockout
/// all produce the same response (HTTP 401).
/// </summary>
public sealed class AuthenticationFailedException() : Exception("Invalid credentials.");
