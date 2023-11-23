using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace App.Authorizer.Test;

public class FunctionTests
{
  [Fact]
  public async Task Should_Throw_When_TokenIsInvalid()
  {
    var request = new APIGatewayCustomAuthorizerRequest
    {
      Headers = new Dictionary<string, string>
      {
        { "authorization", "1234" },
      },
      RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
      {
        RequestId = Guid.NewGuid().ToString(),
      },
    };
    var context = new TestLambdaContext();

    await Assert.ThrowsAsync<UnauthorizedAccessException>(
      async () => await Function.FunctionHandler(request, context));
  }

  [Fact]
  public async Task Should_Throw_When_TokenIsNotSet()
  {
    var request = new APIGatewayCustomAuthorizerRequest
    {
      Headers = new Dictionary<string, string>(),
      RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
      {
        RequestId = Guid.NewGuid().ToString(),
      },
    };
    var context = new TestLambdaContext();

    await Assert.ThrowsAsync<UnauthorizedAccessException>(
      async () => await Function.FunctionHandler(request, context));
  }
}
