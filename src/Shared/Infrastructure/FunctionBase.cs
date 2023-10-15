using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Tracing;

namespace App.Api.Shared.Infrastructure;

public abstract class FunctionBase
{
  private readonly string[] _requiredScopes;
  private static readonly APIGatewayHttpApiV2ProxyResponse _noBodyResponse = new()
  {
    Body = JsonSerializer.Serialize(new[]
    {
      new FunctionError("Body", "Invalid request")
      {
        ErrorCode = "InvalidRequest",
      },
    }, _defaultSerializerOptions),
    StatusCode = (int)HttpStatusCode.BadRequest,
  };
  private static readonly Dictionary<string, string> _defaultHeaders = new()
  {
    { "Content-Type", "application/json" },
    { "Access-Control-Allow-Origin", "http://localhost:3000" },
    { "Access-Control-Allow-Methods", "GET,POST,PATCH,DELETE,OPTIONS,HEAD" },
    { "Access-Control-Allow-Headers", "content-type" }
  };
  private static readonly JsonSerializerOptions _defaultSerializerOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
  };

  public FunctionBase(params string[] requiredScopes) => _requiredScopes = requiredScopes;

  protected abstract Task<APIGatewayHttpApiV2ProxyResponse> Handler(APIGatewayProxyRequest apiGatewayProxyRequest);

  [Logging(LogEvent = true)]
  [Tracing(CaptureMode = TracingCaptureMode.ResponseAndError)]
  public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(
    APIGatewayProxyRequest apiGatewayProxyRequest,
    ILambdaContext context)
  {
    AWSSDKHandler.RegisterXRayForAllServices();
#if DEBUG
    AWSXRayRecorder.Instance.XRayOptions.IsXRayTracingDisabled = true;
#endif

    AppendLookup(apiGatewayProxyRequest);

    if (!HasRequiredScopes(apiGatewayProxyRequest))
    {
      return new()
      {
        Body = JsonSerializer.Serialize(new[]
        {
          new FunctionError("Request", "User not authorized to perform this request")
          {
            ErrorCode = "UnauthorizedRequest",
          },
        }, _defaultSerializerOptions),
        StatusCode = (int)HttpStatusCode.Unauthorized,
      };
    }

    return await Handler(apiGatewayProxyRequest);
  }

  protected T? TryDeserialize<T>(APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    if (string.IsNullOrEmpty(apiGatewayProxyRequest.Body))
    {
      return default;
    }

    return JsonSerializer.Deserialize<T>(
      apiGatewayProxyRequest.Body,
      new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true,
      });
  }

  protected APIGatewayHttpApiV2ProxyResponse Respond<T>(T? response, bool isValid = true)
  {
    if (response == null)
    {
      return _noBodyResponse;
    }

    return new APIGatewayHttpApiV2ProxyResponse
    {
      StatusCode = isValid ? (int)HttpStatusCode.OK : (int)HttpStatusCode.BadRequest,
      Body = JsonSerializer.Serialize(response, _defaultSerializerOptions),
      Headers = _defaultHeaders,
    };
  }

  protected APIGatewayHttpApiV2ProxyResponse Respond(bool isValid = true)
  {
    if (isValid == false)
    {
      return _noBodyResponse;
    }

    return new APIGatewayHttpApiV2ProxyResponse
    {
      StatusCode = (int)HttpStatusCode.NoContent,
      Headers = _defaultHeaders,
    };
  }

  private bool HasRequiredScopes(
    APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    if (_requiredScopes.Any())
    {
      if (apiGatewayProxyRequest.RequestContext.Authorizer.TryGetValue("scope", out var scopes) == false)
      {
        return false;
      }

      if (string.IsNullOrEmpty(scopes.ToString()) || _requiredScopes.Except(scopes.ToString()!.Split(" ")).Any() == true)
      {
        return false;
      }
    }

    return true;
  }

  private void AppendLookup(APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    var requestContextRequestId = apiGatewayProxyRequest.RequestContext.RequestId;
    var lookupInfo = new Dictionary<string, object>()
    {
      { "LookupInfo", new Dictionary<string, object>{{ "LookupId", requestContextRequestId }} },
    };
    Logger.AppendKeys(lookupInfo);
  }
}
