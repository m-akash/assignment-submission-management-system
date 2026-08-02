namespace AssignmentSystem.Shared.Common;

/// <summary>
/// A structured, type-categorized error. Carries a stable code (machine-readable)
/// plus a human-readable message. Created via the static factories so the
/// <see cref="Type"/> is always consistent with intent.
/// </summary>
public sealed class Error
{
    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    private Error(string code, string message, ErrorType type, IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        Code = code;
        Message = message;
        Type = type;
        ValidationErrors = validationErrors;
    }

    // ── Factories ─────────────────────────────────────────────────────────────
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(code, message, ErrorType.Validation, errors);

    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static implicit operator string(Error? error) => error?.Message ?? string.Empty;

    public override string ToString() => $"[{Code}] {Message}";
}
