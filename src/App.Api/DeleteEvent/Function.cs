using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using App.Api.Shared.Extensions;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;

namespace App.Api.DeleteEvent;

public class Function
{
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Function))]
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(APIGatewayProxyRequest))]
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(APIGatewayProxyResponse))]
  static Function()
  { }

  [ExcludeFromCodeCoverage]
  private static async Task Main()
  {
    Func<APIGatewayProxyRequest, ILambdaContext, Task<APIGatewayProxyResponse>> handler = FunctionHandler;
    await LambdaBootstrapBuilder
      .Create(handler, new SourceGeneratorLambdaJsonSerializer<CustomJsonSerializerContext>(options =>
      {
        options.PropertyNameCaseInsensitive = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
      }))
      .Build()
      .RunAsync();
  }

  public static async Task<APIGatewayProxyResponse> FunctionHandler(
    APIGatewayProxyRequest apiGatewayProxyRequest, ILambdaContext context)
  {
    if (!apiGatewayProxyRequest.HasRequiredScopes("event"))
    {
      return FunctionHelpers.UnauthorizedResponse;
    }

    var queryStringParameters = new Dictionary<string, string>(
      apiGatewayProxyRequest.QueryStringParameters ?? new Dictionary<string, string>(),
      StringComparer.OrdinalIgnoreCase);

    queryStringParameters.TryGetValue("id", out var id);

    if (EventMapper.GetKeys(id).Length != 2)
    {
      return FunctionHelpers.NoBodyResponse;
    }

    context.Logger.LogInformation("Attempting to delete event");

    var client = new AmazonDynamoDBClient();
    var keys = EventMapper.GetKeys(id);

    var response = await client.GetItemAsync(new GetItemRequest()
    {
      TableName = Environment.GetEnvironmentVariable("TABLE_NAME")!,
      Key = new Dictionary<string, AttributeValue>()
      {
        { "PartitionKey", new AttributeValue(keys[0]) },
        { "SortKey", new AttributeValue(keys[1]) },
      },
    });

    if (response.Item is not null)
    {
      context.Logger.LogInformation("Matching event foundm, deleting");
      await client.DeleteItemAsync(new DeleteItemRequest()
      {
        TableName = Environment.GetEnvironmentVariable("TABLE_NAME")!,
        Key = new Dictionary<string, AttributeValue>()
        {
          { "PartitionKey", new AttributeValue(keys[0]) },
          { "SortKey", new AttributeValue(keys[1]) },
        },
      });
      await DeleteEventTags(EventMapper.ToDto(response.Item), client);
    }

    return FunctionHelpers.Respond(true);
  }

  public static async Task DeleteEventTags(
    EventDto eventDto, AmazonDynamoDBClient client)
  {
    if (eventDto.Tags is null or { Length: 0 })
    {
      return;
    }

    await client.BatchWriteItemAsync(new BatchWriteItemRequest()
    {
      RequestItems = new Dictionary<string, List<WriteRequest>>()
      {
        {
          Environment.GetEnvironmentVariable("TABLE_NAME")!,
          eventDto.Tags!.Select(tag =>
            new WriteRequest(new DeleteRequest()
            {
              Key = EventTagMapper.FromDto(new()
              {
                AccountId = eventDto.AccountId,
                Date = eventDto.Date,
                Value = tag,
              })
            })).ToList()
        },
      },
    });
  }
}
