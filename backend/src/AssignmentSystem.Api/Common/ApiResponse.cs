using System.Text.Json.Serialization;

namespace AssignmentSystem.Api.Common;

/// <summary>
/// The single success-response envelope for every API endpoint. Errors always use
/// ProblemDetails (RFC 7807) instead, so clients parse exactly two shapes.
/// </summary>
public sealed class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; } = true;

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("pagination")]
    public PaginationMetaData? Pagination { get; init; }
}

public sealed class PaginationMetaData
{
    [JsonPropertyName("page")] public int Page { get; init; }
    [JsonPropertyName("pageSize")] public int PageSize { get; init; }
    [JsonPropertyName("total")] public int Total { get; init; }
    [JsonPropertyName("totalPages")] public int TotalPages { get; init; }
}
