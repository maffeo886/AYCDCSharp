using System.Text.Json.Serialization;
using AYCD.Models;

namespace AYCD.Dtos;

public record CreateTaskDto(
    [property: JsonPropertyName("taskId")]
    string TaskId,
    [property: JsonPropertyName("url")]
    string Url,
    [property: JsonPropertyName("siteKey")]
    string SiteKey,
    [property: JsonPropertyName("version")]
    CaptchaVersion Version,
    [property: JsonPropertyName("action")]
    string? Action = null,
    [property: JsonPropertyName("minScore")]
    float MinScore = 0.0f,
    [property: JsonPropertyName("metaData")]
    Dictionary<string, string>? MetaData = null,
    [property: JsonPropertyName("renderParameters")]
    Dictionary<string, string>? RenderParameters = null,
    [property: JsonPropertyName("proxy")]
    string? Proxy = null,
    [property: JsonPropertyName("proxyRequired")]
    bool ProxyRequired = false,
    [property: JsonPropertyName("userAgent")]
    string? UserAgent = null,
    [property: JsonPropertyName("cookies")]
    string? Cookies = null
);