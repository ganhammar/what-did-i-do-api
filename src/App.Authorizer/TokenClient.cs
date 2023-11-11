using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Tracing;
using Microsoft.IdentityModel.Tokens;

namespace App.Authorizer;

public class TokenClient : ITokenClient
{
  public TokenClient() { }

  [Tracing(SegmentName = "Validate token")]
  public async Task<Result> Validate(AuthorizationOptions authorizationOptions, string token)
  {
    var signingCertificate = new X509Certificate2(Convert.FromBase64String(authorizationOptions.SigningCertificate!));
    var encryptionCertificate = new X509Certificate2(Convert.FromBase64String(authorizationOptions.EncryptionCertificate!));

    token = token.Replace("Bearer ", "");

    var validationParameters = new TokenValidationParameters
    {
      TokenDecryptionKey = new X509SecurityKey(encryptionCertificate),
      ValidateAudience = true,
      ValidateIssuer = true,
      IssuerSigningKey = new X509SecurityKey(signingCertificate),
      ValidateLifetime = true,
      ValidAudiences = authorizationOptions.Audiences,
      ValidIssuer = authorizationOptions.Issuer,
    };
    var tokenHandler = new JwtSecurityTokenHandler();

    var result = await tokenHandler.ValidateTokenAsync(token, validationParameters);

    if (result.IsValid == false)
    {
      Logger.LogError(result.Exception, "Token validation failed");
      throw new UnauthorizedAccessException("Unauthorized");
    }

    return new()
    {
      Email = result.ClaimsIdentity.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value,
      Subject = result.ClaimsIdentity.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value,
      Scope = result.ClaimsIdentity.Claims.FirstOrDefault(x => x.Type == "scope")?.Value,
    };
  }
}
