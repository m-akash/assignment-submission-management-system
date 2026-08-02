namespace AssignmentSystem.Api.Common;

/// <summary>
/// Static key used to stash the correlation id in HttpContext.Items and to expose
/// it to other components (e.g. ProblemDetails factory).
/// </summary>
public static class CorrelationIdContext
{
    public const string Key = "__CorrelationId";
}
