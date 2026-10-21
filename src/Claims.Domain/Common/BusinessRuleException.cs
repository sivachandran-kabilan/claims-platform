namespace Claims.Domain.Common;

/// <summary>The status allows the action but a business rule is broken, e.g. amount above reserve (HTTP 422).</summary>
public sealed class BusinessRuleException(string message) : DomainException(message);
