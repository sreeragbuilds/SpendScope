namespace SpendScope.Application.Abstractions;

/// <summary>The caller of the current request, resolved per request from the access token.</summary>
public interface ICurrentUser
{
    /// <summary><see cref="Guid.Empty"/> when the request is anonymous.</summary>
    Guid UserId { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);

    string? IpAddress { get; }
}
