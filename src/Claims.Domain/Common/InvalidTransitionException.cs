namespace Claims.Domain.Common;

/// <summary>The requested action is not allowed from the claim's current status (HTTP 409).</summary>
public sealed class InvalidTransitionException(string message) : DomainException(message);
