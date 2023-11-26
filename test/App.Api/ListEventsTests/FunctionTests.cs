using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using App.Api.ListEvents;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using TestBase;
using TestBase.Helpers;

namespace ListEventsTests;

[Collection(Constants.DatabaseCollection)]
public class FunctionTests
{
  [Fact]
  public async Task Should_ReturnList_When_InputIsValid()
  {
    var accountId = Guid.NewGuid().ToString();
    EventHelpers.CreateEvent(new()
    {
      AccountId = accountId,
      Title = "Testing Testing",
      Date = DateTime.UtcNow,
    });
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "AccountId", accountId },
      { "Limit", "100" },
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

    Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);

    var body = JsonSerializer.Deserialize<ListEventsResult>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(body);
    Assert.Single(body.Items);
    Assert.Equal(accountId, body.Items.First().AccountId);
  }

  [Fact]
  public async Task Should_ReturnEmptyList_When_ThereIsNoEventsBetweenFromAndTo()
  {
    var accountId = Guid.NewGuid().ToString();
    EventHelpers.CreateEvent(new()
    {
      AccountId = accountId,
      Title = "Testing Testing",
      Date = DateTime.UtcNow,
    });
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "AccountId", accountId },
      { "FromDate", DateTime.UtcNow.AddDays(-7).ToString() },
      { "ToDate", DateTime.UtcNow.AddDays(-6).ToString() },
      { "Limit", "100" },
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

    Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);

    var body = JsonSerializer.Deserialize<ListEventsResult>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(body);
    Assert.Empty(body.Items);
  }

  [Fact]
  public async Task Should_ReturnBadRequest_When_FromDateIsSetAndToDateIsnt()
  {
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "AccountId", Guid.NewGuid().ToString() },
      { "FromDate", DateTime.UtcNow.ToString() },
      { "Limit", "100" },
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
    Assert.Contains(errors, error => error.PropertyName == nameof(ListEventsInput.ToDate)
      && error.ErrorCode == "NotEmpty");
  }

  [Fact]
  public async Task Should_ReturnBadRequest_When_ToDateIsSetAndFromDateIsnt()
  {
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "AccountId", Guid.NewGuid().ToString() },
      { "ToDate", DateTime.UtcNow.ToString() },
      { "Limit", "100" },
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
    Assert.Contains(errors, error => error.PropertyName == nameof(ListEventsInput.FromDate)
      && error.ErrorCode == "NotEmpty");
  }

  [Fact]
  public async Task Should_ReturnBadRequest_When_ToDateIsLessThanFromDate()
  {
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "AccountId", Guid.NewGuid().ToString() },
      { "ToDate", DateTime.UtcNow.AddDays(-1).ToString() },
      { "FromDate", DateTime.UtcNow.ToString() },
      { "Limit", "100" },
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
    Assert.Contains(errors, error => error.PropertyName == nameof(ListEventsInput.ToDate)
      && error.ErrorCode == "InvalidInput");
  }

  [Theory]
  [InlineData("-1", "InvalidInput")]
  [InlineData("201", "InvalidInput")]
  public async Task Should_ReturnBadRequest_When_LimitIsInvalid(string limit, string expectedErrorCode)
  {
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "AccountId", Guid.NewGuid().ToString() },
      { "FromDate", DateTime.UtcNow.AddDays(-7).ToString() },
      { "ToDate", DateTime.UtcNow.AddDays(-6).ToString() },
      { "Limit", limit },
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
    Assert.Contains(errors, error => error.PropertyName == nameof(ListEventsInput.Limit)
      && error.ErrorCode == expectedErrorCode);
  }

  [Fact]
  public async Task Should_ReturnUnauthorized_When_RequiredScopeIsMissing()
  {
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "AccountId", Guid.NewGuid().ToString() },
      { "Limit", "100" },
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

  [Fact]
  public async Task Should_FilterByTags_When_InputContainsTagsFilter()
  {
    var accountId = Guid.NewGuid().ToString();
    EventHelpers.CreateEvent(new()
    {
      AccountId = accountId,
      Title = "Testing Testing",
      Date = DateTime.UtcNow,
      Tags = ["test"],
    });
    EventHelpers.CreateEvent(new()
    {
      AccountId = accountId,
      Title = "Testing 2 Testing 2",
      Date = DateTime.UtcNow,
      Tags = ["testing"],
    });

    var function = new Function();
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "AccountId", accountId },
      { "Limit", "100" },
      { "Tag", "test" },
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

    Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);

    var body = JsonSerializer.Deserialize<ListEventsResult>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(body);
    Assert.Single(body.Items);
    Assert.Equal(accountId, body.Items.First().AccountId);
    Assert.NotNull(body.Items.First().Tags);
    Assert.Single(body.Items.First().Tags!);
    Assert.Equal("test", body.Items.First()!.Tags!.First());
  }

  [Theory]
  [InlineData(default)]
  [InlineData("test")]
  [InlineData("testing")]
  public async Task Should_ReturnNextPage_When_CalledWithPaginationToken(string? tag)
  {
    var accountId = Guid.NewGuid().ToString();
    EventHelpers.CreateEvent(new()
    {
      AccountId = accountId,
      Title = "Testing Testing",
      Date = DateTime.UtcNow,
      Tags = ["test", "testing"],
    });
    EventHelpers.CreateEvent(new()
    {
      AccountId = accountId,
      Title = "Testing 2 Testing 2",
      Date = DateTime.UtcNow,
      Tags = ["test"],
    });
    EventHelpers.CreateEvent(new()
    {
      AccountId = accountId,
      Title = "Testing 3 Testing 3",
      Date = DateTime.UtcNow,
      Tags = ["testing"],
    });

    static async Task<APIGatewayProxyResponse> getResponse(Dictionary<string, string> data)
    {
      var context = new TestLambdaContext();
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
      return await Function.FunctionHandler(request, context);
    }

    var data = new Dictionary<string, string>
    {
      { "AccountId", accountId },
      { "Limit", "1" },
    };

    if (tag != default)
    {
      data.Add("Tag", tag);
    }

    var firstPage = await getResponse(data);

    Assert.Equal((int)HttpStatusCode.OK, firstPage.StatusCode);

    var firstPageBody = JsonSerializer.Deserialize<ListEventsResult>(firstPage.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(firstPageBody);
    Assert.Single(firstPageBody.Items);
    Assert.NotNull(firstPageBody.PaginationToken);

    data.Add("PaginationToken", firstPageBody.PaginationToken);

    var secondPage = await getResponse(data);

    Assert.Equal((int)HttpStatusCode.OK, secondPage.StatusCode);

    var secondPageBody = JsonSerializer.Deserialize<ListEventsResult>(secondPage.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(secondPageBody);
    Assert.Single(secondPageBody.Items);
    Assert.NotEqual(firstPageBody.Items.First().Id, secondPageBody.Items.First().Id);
  }
}
