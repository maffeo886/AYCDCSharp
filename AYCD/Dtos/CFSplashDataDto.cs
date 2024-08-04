using System.Text.Json.Serialization;

namespace AYCD.Dtos;

public record CfSplashDataDto(
    [property: JsonPropertyName("cf_clearance")]
    string? CfClearance,
    [property: JsonPropertyName("currentUrl")]
    string? CurrentUrl,
    [property: JsonPropertyName("challenge")]
    ChallengeDto Challenge
);