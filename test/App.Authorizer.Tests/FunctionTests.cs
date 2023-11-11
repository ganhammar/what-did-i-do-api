using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace App.Authorizer.Test;

public class FunctionTests
{
  [Fact]
  public async Task Should_Allow_When_TokenIsValid()
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
    var function = GetFunction();

    var result = await function.FunctionHandler(request, context);

    Assert.Equal("Allow", result.PolicyDocument.Statement.First().Effect);
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
    var function = GetFunction();

    await Assert.ThrowsAsync<UnauthorizedAccessException>(
      async () => await function.FunctionHandler(request, context));
  }

  private static Function GetFunction()
  {
    var function = new Mock<Function>();
    function
      .Protected()
      .Setup("ConfigureServices", ItExpr.IsAny<IServiceCollection>())
      .Callback((IServiceCollection services) =>
      {
        var mockedTokenClient = new Mock<ITokenClient>();
        mockedTokenClient
          .Setup(x => x.Validate(It.IsAny<AuthorizationOptions>(), It.IsAny<string>()))
          .Returns(Task.FromResult(new Result
          {
            Scope = "test",
            Subject = "123",
            Email = "test@wdid.fyi",
          }));
        services.AddSingleton(mockedTokenClient.Object);

        var mockedOptions = new Mock<IOptionsMonitor<AuthorizationOptions>>();
        mockedOptions.Setup(x => x.CurrentValue).Returns(new AuthorizationOptions());
        services.AddSingleton(mockedOptions.Object);
      });

    return function.Object;
  }
}
