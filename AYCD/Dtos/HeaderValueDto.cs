using System.Text.Json.Serialization;

namespace AYCD.Dtos;

public record HeaderValueDto(
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("value")]
    string Value    
);