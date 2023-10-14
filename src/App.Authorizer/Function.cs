using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using AWS.Lambda.Powertools.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

[assembly: LambdaSerializer(typeof(CamelCaseLambdaJsonSerializer))]

namespace App.Authorizer;

public class Function
{
  private readonly IConfiguration Configuration;
  private readonly IServiceProvider ServiceProvider;

  protected virtual void ConfigureServices(IServiceCollection services)
  {
    services.Configure<AuthorizationOptions>(Configuration.GetSection(nameof(AuthorizationOptions)));
    services.AddMemoryCache();
    services.AddHttpClient<ITokenClient, TokenClient>();
  }

  public Function()
  {
    Configuration = new ConfigurationBuilder()
      .SetBasePath(Directory.GetCurrentDirectory())
      .AddJsonFile("appsettings.json", optional: true)
      .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
      .AddSystemsManager("/WDID/Authorizer")
      .Build();

    var services = new ServiceCollection();
    ConfigureServices(services);
    ServiceProvider = services.BuildServiceProvider();
  }

  [Logging(LogEvent = true)]
  public async Task<APIGatewayCustomAuthorizerResponse> FunctionHandler(
    APIGatewayCustomAuthorizerRequest request, ILambdaContext _)
  {
    AppendLookup(request.RequestContext.RequestId);

    var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);

    headers.TryGetValue("authorization", out var token);

    var options = ServiceProvider.GetRequiredService<IOptionsMonitor<AuthorizationOptions>>();
    var tokenClient = ServiceProvider.GetRequiredService<ITokenClient>();

    if (token == default)
    {
      throw new UnauthorizedAccessException("Unauthorized");
    }

    var result = await tokenClient.Validate(options.CurrentValue, token);

    if (result.Active == false)
    {
      throw new UnauthorizedAccessException("Unauthorized");
    }

    return new()
    {
      PrincipalID = "user",
      PolicyDocument = new()
      {
        Version = "2012-10-17",
        Statement = new()
        {
          new()
          {
            Effect = "Allow",
            Resource = new() { request.MethodArn },
            Action = new() { "execute-api:Invoke" },
          },
        },
      },
      Context = new()
      {
        { "scope", result.Scope },
        { "sub", result.Subject },
        { "email", result.Email },
      },
    };
  }

  private void AppendLookup(string lookupId)
  {
    var lookupInfo = new Dictionary<string, object>()
    {
      { "LookupInfo", new Dictionary<string, object>{{ "LookupId", lookupId }} },
    };
    Logger.AppendKeys(lookupInfo);
  }
}
