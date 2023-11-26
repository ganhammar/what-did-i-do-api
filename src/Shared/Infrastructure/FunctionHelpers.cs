using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Amazon.Lambda.APIGatewayEvents;

namespace App.Api.Shared.Infrastructure;

public static class FunctionHelpers
{
  public static APIGatewayProxyResponse UnauthorizedResponse =>
    new()
    {
      Body = JsonSerializer.Serialize(new[]
      {
        new FunctionError("Request", "User not authorized to perform this request")
        {
          ErrorCode = "UnauthorizedRequest",
        },
      }, CustomJsonSerializerContext.Default.FunctionErrorArray),
      StatusCode = (int)HttpStatusCode.Unauthorized,
    };

  public static APIGatewayProxyResponse NoBodyResponse =>
    new()
    {
      Body = JsonSerializer.Serialize(new[]
      {
        new FunctionError("Body", "Invalid request")
        {
          ErrorCode = "InvalidRequest",
        },
      }, CustomJsonSerializerContext.Default.FunctionErrorArray),
      StatusCode = (int)HttpStatusCode.BadRequest,
    };

  public static APIGatewayProxyResponse Respond<T>(T? response, JsonTypeInfo jsonTypeInfo, bool isValid = true)
  {
    if (response == null)
    {
      return NoBodyResponse;
    }

    return new APIGatewayProxyResponse
    {
      StatusCode = isValid ? (int)HttpStatusCode.OK : (int)HttpStatusCode.BadRequest,
      Body = JsonSerializer.Serialize(response, jsonTypeInfo),
      Headers = _defaultHeaders,
    };
  }

  public static APIGatewayProxyResponse Respond(bool isValid = true)
  {
    if (isValid == false)
    {
      return NoBodyResponse;
    }

    return new APIGatewayProxyResponse
    {
      StatusCode = (int)HttpStatusCode.NoContent,
      Headers = _defaultHeaders,
    };
  }

  private static readonly Dictionary<string, string> _defaultHeaders = new()
  {
    { "Content-Type", "application/json" },
    { "Access-Control-Allow-Origin", "http://localhost:3000" },
    { "Access-Control-Allow-Methods", "GET,POST,PATCH,DELETE,OPTIONS,HEAD" },
    { "Access-Control-Allow-Headers", "content-type" }
  };
}
