using System.Text.Json.Serialization;

namespace AYCD.Dtos;

public record CancelTaskRequest(
    [property: JsonPropertyName("taskIds")]
    List<string> TaskIds,
    [property: JsonPropertyName("responseRequired")]
    bool ResponseRequired
);