using System.Text.Json.Serialization;

namespace App.Authorizer;

public class Result
{
  [JsonPropertyName("sub")]
  public string? Subject { get; set; }
  [JsonPropertyName("scope")]
  public string? Scope { get; set; }
  [JsonPropertyName("email")]
  public string? Email { get; set; }
}
