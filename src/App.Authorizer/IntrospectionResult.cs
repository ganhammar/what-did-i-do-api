using System.Text.Json.Serialization;

namespace App.Authorizer;

public class IntrospectionResult
{
  [JsonPropertyName("sub")]
  public string? Subject { get; set; }
  [JsonPropertyName("scope")]
  public string? Scope { get; set; }
  [JsonPropertyName("email")]
  public string? Email { get; set; }
}
