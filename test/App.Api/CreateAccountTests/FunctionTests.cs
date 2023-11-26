using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using App.Api.CreateAccount;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using TestBase;

namespace CreateAccountTests;

[Collection(Constants.DatabaseCollection)]
public class FunctionTests
{
  [Fact]
  public async Task Should_ReturnAccount_When_InputIsValid()
  {
    var context = new TestLambdaContext();
    var data = new CreateAccountInput
    {
      Name = "Testing Testing",
    };
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      Body = JsonSerializer.Serialize(data, new JsonSerializerOptions()
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      }),
      RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
      {
        RequestId = Guid.NewGuid().ToString(),
        Authorizer = new()
        {
          { "scope", "email test account" },
          { "sub", Guid.NewGuid() },
          { "email", "test@wdid.fyi" },
        },
      },
    };
    var response = await Function.FunctionHandler(request, context);

    Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);

    var body = JsonSerializer.Deserialize<AccountDto>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(body);
    Assert.Equal(data.Name, body!.Name);
    Assert.NotNull(body!.Id);
  }

  [Fact]
  public async Task Should_ReturnBadRequest_When_BodyIsEmpty()
  {
    var context = new TestLambdaContext();
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
      {
        RequestId = Guid.NewGuid().ToString(),
        Authorizer = new()
        {
          { "scope", "email test account" },
          { "sub", Guid.NewGuid() },
          { "email", "test@wdid.fyi" },
        },
      },
    };
    var response = await Function.FunctionHandler(request, context);

    Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);

    var errors = JsonSerializer.Deserialize<List<FunctionError>>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(errors);
    Assert.Contains(errors, error => error.ErrorCode == "InvalidRequest");
  }

  [Fact]
  public async Task Should_ReturnUnauthorized_When_ScopeIsNotSet()
  {
    var context = new TestLambdaContext();
    var data = new CreateAccountInput
    {
      Name = "Testing Testing",
    };
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      Body = JsonSerializer.Serialize(data, new JsonSerializerOptions()
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      }),
      RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
      {
        RequestId = Guid.NewGuid().ToString(),
        Authorizer = new()
        {
          { "sub", Guid.NewGuid() },
          { "email", "test@wdid.fyi" },
        },
      },
    };
    var response = await Function.FunctionHandler(request, context);

    Assert.Equal((int)HttpStatusCode.Unauthorized, response.StatusCode);

    var errors = JsonSerializer.Deserialize<List<FunctionError>>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(errors);
    Assert.Contains(errors, error => error.ErrorCode == "UnauthorizedRequest");
  }

  [Fact]
  public async Task Should_ReturnUnauthorized_When_RequiredScopeIsMissing()
  {
    var context = new TestLambdaContext();
    var data = new CreateAccountInput
    {
      Name = "Testing Testing",
    };
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      Body = JsonSerializer.Serialize(data, new JsonSerializerOptions()
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      }),
      RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
      {
        RequestId = Guid.NewGuid().ToString(),
        Authorizer = new()
        {
          { "scope", "email test event" },
          { "sub", Guid.NewGuid() },
          { "email", "test@wdid.fyi" },
        },
      },
    };
    var response = await Function.FunctionHandler(request, context);

    Assert.Equal((int)HttpStatusCode.Unauthorized, response.StatusCode);

    var errors = JsonSerializer.Deserialize<List<FunctionError>>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(errors);
    Assert.Contains(errors, error => error.ErrorCode == "UnauthorizedRequest");
  }

  [Fact]
  public async Task Should_Throw_When_SubIsMissingInAuthorizerContext()
  {
    var context = new TestLambdaContext();
    var data = new CreateAccountInput
    {
      Name = "Testing Testing",
    };
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      Body = JsonSerializer.Serialize(data, new JsonSerializerOptions()
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      }),
      RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
      {
        RequestId = Guid.NewGuid().ToString(),
        Authorizer = new()
        {
          { "scope", "email test account" },
          { "email", "test@wdid.fyi" },
        },
      },
    };

    var response = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await Function.FunctionHandler(request, context));

    Assert.Equal("Subject", response.ParamName);
  }

  [Fact]
  public async Task Should_Throw_When_EmailIsMissingInAuthorizerContext()
  {
    var context = new TestLambdaContext();
    var data = new CreateAccountInput
    {
      Name = "Testing Testing",
    };
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      Body = JsonSerializer.Serialize(data, new JsonSerializerOptions()
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      }),
      RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
      {
        RequestId = Guid.NewGuid().ToString(),
        Authorizer = new()
        {
          { "scope", "email test account" },
          { "sub", Guid.NewGuid() },
        },
      },
    };

    var response = await Assert.ThrowsAsync<ArgumentNullException>(async () => await Function.FunctionHandler(request, context));

    Assert.Equal("Email", response.ParamName);
  }
}
