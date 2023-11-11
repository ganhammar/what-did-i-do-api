namespace App.Authorizer;

public class AuthorizationOptions
{
  public string? Issuer { get; set; }
  public List<string>? Audiences { get; set; }
  public string? SigningCertificate { get; set; }
  public string? EncryptionCertificate { get; set; }
}
