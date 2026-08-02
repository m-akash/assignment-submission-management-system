namespace AssignmentSystem.Shared.Common;

/// <summary>
/// Category of a domain/application error. Drives the HTTP status code the API maps
/// each <see cref="Error"/> to (see ResultExtensions in the Api layer).
/// </summary>
public enum ErrorType
{
    /// <summary>400 — malformed / semantically wrong request that isn't validation.</summary>
    Failure = 0,

    /// <summary>Validation: 422.</summary>
    Validation = 1,

    /// <summary>404 — resource not found.</summary>
    NotFound = 2,

    /// <summary>401 — not authenticated.</summary>
    Unauthorized = 3,

    /// <summary>403 — authenticated but not allowed.</summary>
    Forbidden = 4,

    /// <summary>409 — concurrent/conflicting change.</summary>
    Conflict = 5,
}
