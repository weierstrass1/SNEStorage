using System.Text.Json.Serialization;
namespace SNEStorage.DTOs;
public sealed class TurnstileResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }
}
