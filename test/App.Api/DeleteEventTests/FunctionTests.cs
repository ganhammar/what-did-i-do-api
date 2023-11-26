using System.Net;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using App.Api.DeleteEvent;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using TestBase;
using TestBase.Helpers;

namespace CreateEventTests;

[Collection(Constants.DatabaseCollection)]
public class FunctionTests
{
  [Fact]
  public async Task Should_ReturnSuccessfully_When_InputIsValid()
  {
    var item = EventHelpers.CreateEvent(new()
    {
      AccountId = Guid.NewGuid().ToString(),
      Title = "Testing Testing",
      Date = DateTime.UtcNow,
    });

    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "Id", EventMapper.ToDto(item).Id! },
    };
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      QueryStringParameters = data,
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

    Assert.Equal((int)HttpStatusCode.NoContent, response.StatusCode);
  }

  [Fact]
  public async Task Should_DeleteTags_When_InputIsValid()
  {
    var accountId = Guid.NewGuid().ToString();
    var date = DateTime.UtcNow;
    var tags = new[] { "test", "testing" };
    var item = EventHelpers.CreateEvent(new()
    {
      AccountId = accountId,
      Title = "Testing Testing",
      Date = date,
      Tags = tags,
    });

    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "Id", EventMapper.ToDto(item).Id! },
    };
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      QueryStringParameters = data,
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

    Assert.Equal((int)HttpStatusCode.NoContent, response.StatusCode);

    var client = new AmazonDynamoDBClient();
    var tableName = Environment.GetEnvironmentVariable("TABLE_NAME")!;

    var result = await client.BatchGetItemAsync(new BatchGetItemRequest()
    {
      RequestItems = new()
      {
        {
          tableName,
          new KeysAndAttributes()
          {
            Keys = tags.Select(tag => new Dictionary<string, AttributeValue>()
            {
              { "PartitionKey", new AttributeValue(EventTagMapper.GetPartitionKey(accountId)) },
              { "SortKey", new AttributeValue(EventTagMapper.GetSortKey(tag, date)) },
            }).ToList(),
          }
        },
      },
    });

    Assert.Empty(result.Responses[tableName]);
  }

  [Fact]
  public async Task Should_ReturnBadRequest_When_InputIsNotValid()
  {
    var context = new TestLambdaContext();
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      QueryStringParameters = new Dictionary<string, string>(),
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

    Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);

    var errors = JsonSerializer.Deserialize<List<FunctionError>>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(errors);
    Assert.Contains(errors, error => error.PropertyName == "Body"
      && error.ErrorCode == "InvalidRequest");
  }

  [Fact]
  public async Task Should_ReturnBadRequest_When_IdIsNotValid()
  {
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "Id", "not-the-real-deal" },
    };
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      QueryStringParameters = data,
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

    Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);

    var errors = JsonSerializer.Deserialize<List<FunctionError>>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(errors);
    Assert.Contains(errors, error => error.PropertyName == "Body"
      && error.ErrorCode == "InvalidRequest");
  }

  [Fact]
  public async Task Should_ReturnUnauthorized_When_RequiredScopeIsMissing()
  {
    var item = EventHelpers.CreateEvent(new()
    {
      AccountId = Guid.NewGuid().ToString(),
      Title = "Testing Testing",
      Date = DateTime.UtcNow,
    });

    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "Id", EventMapper.ToDto(item).Id! },
    };
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      QueryStringParameters = data,
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
