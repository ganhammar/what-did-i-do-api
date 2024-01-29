using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.SimpleSystemsManagement;
using Microsoft.IdentityModel.Tokens;

namespace App.Authorizer;

public class Function
{
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Function))]
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(APIGatewayCustomAuthorizerRequest))]
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(APIGatewayCustomAuthorizerResponse))]
  static Function()
  { }

  private static async Task Main()
  {
    Func<APIGatewayCustomAuthorizerRequest, ILambdaContext, Task<APIGatewayCustomAuthorizerResponse>> handler = FunctionHandler;
    await LambdaBootstrapBuilder
      .Create(handler, new SourceGeneratorLambdaJsonSerializer<CustomJsonSerializerContext>(options =>
      {
        options.PropertyNameCaseInsensitive = true;
      }))
      .Build()
      .RunAsync();
  }

  public static async Task<APIGatewayCustomAuthorizerResponse> FunctionHandler(
    APIGatewayCustomAuthorizerRequest request, ILambdaContext _)
  {
    var client = new AmazonSimpleSystemsManagementClient();
    var parameters = await client.GetParametersByPathAsync(new()
    {
      Path = "/WDID/AuthorizerTest/AuthorizationOptions",
    });
    var options = new AuthorizationOptions
    {
      SigningCertificate = parameters.Parameters
        .Where(x => x.Name.EndsWith("SigningCertificate"))
        .Select(x => x.Value).FirstOrDefault(),
      EncryptionCertificate = parameters.Parameters
        .Where(x => x.Name.EndsWith("EncryptionCertificate"))
        .Select(x => x.Value).FirstOrDefault(),
      Issuers = ["https://www.wdid.fyi/", "http://localhost:3001/"],
      Audiences = ["what-did-i-do.web-client"],
    };

    var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);

    headers.TryGetValue("authorization", out var token);

    if (token == default)
    {
      throw new UnauthorizedAccessException("Unauthorized");
    }

    var result = await Validate(options, token);
    var methodArnParts = request.MethodArn.Split(':');
    var apiGatewayArnParts = methodArnParts[5].Split('/');

    return new()
    {
      PrincipalID = "user",
      PolicyDocument = new()
      {
        Version = "2012-10-17",
        Statement =
        [
          new()
          {
            Effect = "Allow",
            Resource = [
              $"{methodArnParts[0]}:{methodArnParts[1]}:{methodArnParts[2]}:{methodArnParts[3]}:{methodArnParts[4]}:{apiGatewayArnParts[0]}/*/*",
            ],
            Action = ["execute-api:Invoke"],
          },
        ],
      },
      Context = new()
      {
        { "scope", result.Scope },
        { "sub", result.Subject },
        { "email", result.Email },
      },
    };
  }

  private static async Task<Result> Validate(AuthorizationOptions authorizationOptions, string token)
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
      ValidIssuers = authorizationOptions.Issuers,
    };
    var tokenHandler = new JwtSecurityTokenHandler();

    var result = await tokenHandler.ValidateTokenAsync(token, validationParameters);

    if (result.IsValid == false)
    {
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
