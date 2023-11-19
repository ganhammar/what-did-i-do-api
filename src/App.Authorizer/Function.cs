﻿using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Tracing;
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
    services.AddSingleton<ITokenClient, TokenClient>();
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
  [Tracing(CaptureMode = TracingCaptureMode.ResponseAndError)]
  public async Task<APIGatewayCustomAuthorizerResponse> FunctionHandler(
    APIGatewayCustomAuthorizerRequest request, ILambdaContext _)
  {
    AWSSDKHandler.RegisterXRayForAllServices();
#if DEBUG
    AWSXRayRecorder.Instance.XRayOptions.IsXRayTracingDisabled = true;
#endif

    var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);

    headers.TryGetValue("authorization", out var token);

    var options = ServiceProvider.GetRequiredService<IOptionsMonitor<AuthorizationOptions>>();
    var tokenClient = ServiceProvider.GetRequiredService<ITokenClient>();

    if (token == default)
    {
      throw new UnauthorizedAccessException("Unauthorized");
    }

    var result = await tokenClient.Validate(options.CurrentValue, token);
    var methodArnParts = request.MethodArn.Split(':');
    var apiGatewayArnParts = methodArnParts[5].Split('/');

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
            Resource = new() {
              $"{methodArnParts[0]}:{methodArnParts[1]}:{methodArnParts[2]}:{methodArnParts[3]}:{methodArnParts[4]}:{apiGatewayArnParts[0]}/*/*"
            },
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
}
