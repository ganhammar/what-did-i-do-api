namespace App.Authorizer;

public class AuthorizationOptions
{
  public List<string>? Issuers { get; set; }
  public List<string>? Audiences { get; set; }
  public string? SigningCertificate { get; set; }
  public string? EncryptionCertificate { get; set; }
}
