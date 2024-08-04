using System.Text.Json.Serialization;

namespace AYCD.Dtos;

public record TaskResponseDto(
    [property: JsonPropertyName("taskId")]
    string? TaskId,
    [property: JsonPropertyName("createdAt")]
    long CreatedAt,
    [property: JsonPropertyName("token")] 
    string? Token,
    [property: JsonPropertyName("status")] 
    string? Status
);