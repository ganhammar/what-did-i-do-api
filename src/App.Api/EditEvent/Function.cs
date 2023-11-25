using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using App.Api.Shared.Extensions;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using AWS.Lambda.Powertools.Logging;

namespace App.Api.EditEvent;

public class Function
{
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Function))]
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(APIGatewayProxyRequest))]
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(APIGatewayProxyResponse))]
  static Function()
  {
    // AWSSDKHandler.RegisterXRayForAllServices();
  }

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
    else if (string.IsNullOrEmpty(apiGatewayProxyRequest.Body))
    {
      return FunctionHelpers.NoBodyResponse;
    }

    var request = JsonSerializer.Deserialize(
      apiGatewayProxyRequest.Body,
      CustomJsonSerializerContext.Default.EditEventInput);

    if (request is null)
    {
      return FunctionHelpers.NoBodyResponse;
    }

    context.Logger.LogInformation("Attempting to edit event");

    var client = new AmazonDynamoDBClient();
    var config = new DynamoDBOperationConfig()
    {
      OverrideTableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
    };
    var item = EventMapper.FromDtoDD(new EventDto
    {
      Id = request.Id,
    });

    var response = await client.GetItemAsync(new GetItemRequest()
    {
      TableName = Environment.GetEnvironmentVariable("TABLE_NAME")!,
      Key = new Dictionary<string, AttributeValue>()
      {
        { "PartitionKey", new AttributeValue(item["PartitionKey"].S) },
        { "SortKey", new AttributeValue(item["SortKey"].S) },
      },
    });

    item = response.Item;

    var eventDto = EventMapper.ToDtoDD(item);
    await SaveTags(eventDto, request.Tags, client, context);

    item["Title"] = new AttributeValue(request.Title);

    if (request.Description is not null)
    {
      item["Description"] = new AttributeValue(request.Description);
    }
    else
    {
      item.Remove("Description");
    }

    if (request.Tags is not null and { Length: > 0 })
    {
      item["Tags"] = new AttributeValue(request.Tags.Distinct().ToList());
    }
    else
    {
      item.Remove("Tags");
    }

    await client.PutItemAsync(new PutItemRequest()
    {
      TableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
      Item = item,
    });

    context.Logger.LogInformation("Event editd");

    eventDto = EventMapper.ToDtoDD(item);

    return FunctionHelpers.Respond(
      eventDto,
      CustomJsonSerializerContext.Default.EventDto);
  }

  public static async Task SaveTags(
    EventDto item, string[]? newTags, AmazonDynamoDBClient client, ILambdaContext context)
  {
    if ((item.Tags is null or { Length: 0 }) && (newTags is null or { Length: 0 }))
    {
      return;
    }

    var config = new DynamoDBOperationConfig()
    {
      OverrideTableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
    };

    Logger.LogInformation($"Attempting to update tag(s)");

    // Delete old tags that no longer applies
    if (item.Tags is not null and { Length: > 0 })
    {
      await client.BatchWriteItemAsync(new BatchWriteItemRequest()
      {
        RequestItems = new()
        {
          {
            Environment.GetEnvironmentVariable("TABLE_NAME")!,
            item.Tags!.Select(tag =>
              new WriteRequest(new DeleteRequest()
              {
                Key = EventTagMapper.FromDtoDD(new()
                {
                  AccountId = item.AccountId,
                  Date = item.Date,
                  Value = tag,
                })
              })).ToList()
          },
        },
      });
    }

    // Create or update
    if (newTags is not null and { Length: > 0 })
    {
      await client.BatchWriteItemAsync(new BatchWriteItemRequest()
      {
        RequestItems = new()
        {
          {
            Environment.GetEnvironmentVariable("TABLE_NAME")!,
            newTags.SelectMany(value =>
              new List<WriteRequest>
              {
                new(new PutRequest()
                {
                  Item = TagMapper.FromDtoDD(new TagDto
                  {
                    AccountId = item.AccountId,
                    Value = value,
                  }),
                }),
                new(new PutRequest()
                {
                  Item = EventTagMapper.FromDtoDD(new EventTagDto
                  {
                    AccountId = item.AccountId,
                    Date = item.Date,
                    Value = value,
                  }),
                }),
              }).ToList()
          },
        },
      });
    }

    context.Logger.LogInformation("Tags saved");
  }
}
