using System.Text.Json.Serialization;

namespace AYCD.Dtos;

public record AuthResponse(
    [property: JsonPropertyName("token")]
    string Token,
    
    [property: JsonPropertyName("expiresAt")]
    long ExpiresAt    
);