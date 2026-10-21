namespace Claims.Application.Common.Security;

/// <summary>The authenticated caller, read from the validated token (never from the request body).</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    string Role { get; }
}
