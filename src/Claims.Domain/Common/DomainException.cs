namespace Claims.Domain.Common;

/// <summary>Base type for rule violations raised by the domain. Mapped to ProblemDetails by the API.</summary>
public abstract class DomainException(string message) : Exception(message);
