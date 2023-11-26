using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using App.Api.CreateEvent;
using App.Api.Shared.Extensions;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using TestBase;

namespace CreateEventTests;

[Collection(Constants.DatabaseCollection)]
public class FunctionTests
{
  [Fact]
  public async Task Should_ReturnEvent_When_InputIsValid()
  {
    var context = new TestLambdaContext();
    var data = new CreateEventInput
    {
      AccountId = "test-account",
      Title = "Testing Testing",
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

    Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);

    var body = JsonSerializer.Deserialize<EventDto>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(body);
    Assert.Equal(data.Title, body!.Title);
    Assert.NotNull(body!.Id);
  }

  [Fact]
  public async Task Should_ReturnEventWithTags_When_InputIsValid()
  {
    var context = new TestLambdaContext();
    var tags = new[] { "test", "testing" };
    var data = new CreateEventInput
    {
      AccountId = "test-account",
      Title = "Testing Testing",
      Tags = tags,
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

    Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);

    var body = JsonSerializer.Deserialize<EventDto>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(body);
    Assert.Equal(data.Title, body!.Title);
    Assert.NotNull(body!.Id);
    Assert.NotNull(body.Tags);
    Assert.Equal(2, body.Tags.Count());
    Assert.Contains(body.Tags, (tag) => tags.Contains(tag));
  }

  [Fact]
  public async Task Should_RemoveDuplicateTags_When_InputContainsDuplicateTagValues()
  {
    var context = new TestLambdaContext();
    var tags = new[] { "test", "test" };
    var data = new CreateEventInput
    {
      AccountId = "test-account",
      Title = "Testing Testing",
      Tags = tags,
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

    Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);

    var body = JsonSerializer.Deserialize<EventDto>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(body);
    Assert.Equal(data.Title, body!.Title);
    Assert.NotNull(body!.Id);
    Assert.NotNull(body.Tags);
    Assert.Single(body.Tags);
  }

  [Fact]
  public async Task Should_ReturnEvent_When_InputIsValidWithDate()
  {
    var context = new TestLambdaContext();
    var date = DateTime.Now.AddDays(-1337).ToUniversalTime();
    var data = new CreateEventInput
    {
      AccountId = "test-account",
      Title = "Testing Testing",
      Date = date,
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

    Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);

    var body = JsonSerializer.Deserialize<EventDto>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(body);
    Assert.Equal(data.Title, body!.Title);
    Assert.NotNull(body!.Id);
    Assert.Equal(date.ToUniversalString(), body!.Date.ToUniversalString());
  }

  [Fact]
  public async Task Should_ReturnUnauthorized_When_RequiredScopeIsMissing()
  {
    var context = new TestLambdaContext();
    var data = new CreateEventInput
    {
      Title = "Testing Testing",
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

    Assert.Equal((int)HttpStatusCode.Unauthorized, response.StatusCode);

    var errors = JsonSerializer.Deserialize<List<FunctionError>>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(errors);
    Assert.Contains(errors, error => error.ErrorCode == "UnauthorizedRequest");
  }
}
