using Amazon.Lambda.APIGatewayEvents;

namespace App.Api.Shared.Extensions;

public static class APIGatewayProxyRequestExtensions
{
  public static string? GetSubject(this APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    if (apiGatewayProxyRequest.RequestContext.Authorizer.TryGetValue("sub", out var subject))
    {
      return subject.ToString();
    }

    return default;
  }

  public static string? GetEmail(this APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    if (apiGatewayProxyRequest.RequestContext.Authorizer.TryGetValue("email", out var email))
    {
      return email.ToString();
    }

    return default;
  }

  public static bool HasRequiredScopes(
    this APIGatewayProxyRequest apiGatewayProxyRequest, params string[] requiredScopes)
  {
    if (requiredScopes.Length == 0)
    {
      if (apiGatewayProxyRequest.RequestContext.Authorizer.TryGetValue("scope", out var scopes) == false)
      {
        return false;
      }

      if (string.IsNullOrEmpty(scopes.ToString()) || requiredScopes.Except(scopes.ToString()!.Split(" ")).Any() == true)
      {
        return false;
      }
    }

    return true;
  }
}
