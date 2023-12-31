﻿using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using App.Api.ListTags;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using TestBase;
using TestBase.Helpers;

namespace ListTagsTests;

[Collection(Constants.DatabaseCollection)]
public class FunctionTests
{
  [Fact]
  public async Task Should_ReturnList_When_InputIsValid()
  {
    var accountId = Guid.NewGuid().ToString();
    TagHelpers.CreateTags(accountId, "test", "testing");
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "AccountId", accountId },
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

    var body = JsonSerializer.Deserialize<List<TagDto>>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(body);
    Assert.Equal(2, body.Count());
    Assert.Equal(accountId, body.First().AccountId);
    Assert.Contains(body, (tag) => new[] { "test", "testing" }.Contains(tag.Value));
  }

  [Fact]
  public async Task Should_ReturnEmptyList_When_ThereIsNoTags()
  {
    var accountId = Guid.NewGuid().ToString();
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "AccountId", accountId },
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

    var body = JsonSerializer.Deserialize<List<TagDto>>(response.Body, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true,
    });

    Assert.NotNull(body);
    Assert.Empty(body);
  }

  [Fact]
  public async Task Should_ReturnUnauthorized_When_RequiredScopeIsMissing()
  {
    var context = new TestLambdaContext();
    var data = new Dictionary<string, string>
    {
      { "AccountId", Guid.NewGuid().ToString() },
    };
    var request = new APIGatewayProxyRequest
    {
      HttpMethod = HttpMethod.Post.Method,
      Body = JsonSerializer.Serialize(data),
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
