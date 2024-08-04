using System.Text.Json.Serialization;

namespace AYCD.Dtos;

public record ChallengeDto(
    [property: JsonPropertyName("requestUrl")]
    string RequestUrl,
    [property: JsonPropertyName("requestHeaders")]
    List<HeaderValueDto> RequestHeaders,
    [property: JsonPropertyName("responseUrl")]
    string ResponseUrl,
    [property: JsonPropertyName("responseHeaders")]
    List<HeaderValueDto> ResponseHeaders
);