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

namespace App.Api.CreateEvent;

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
    else if (string.IsNullOrEmpty(apiGatewayProxyRequest.Body))
    {
      return FunctionHelpers.NoBodyResponse;
    }

    var request = JsonSerializer.Deserialize(
      apiGatewayProxyRequest.Body,
      CustomJsonSerializerContext.Default.CreateEventInput);

    if (request is null)
    {
      return FunctionHelpers.NoBodyResponse;
    }

    var client = new AmazonDynamoDBClient();
    var item = EventMapper.FromDto(new EventDto
    {
      AccountId = request.AccountId,
      Title = request.Title,
      Description = request.Description,
      Date = request.Date?.ToUniversalTime() ?? DateTime.UtcNow,
      Tags = request.Tags?.Distinct().ToArray(),
    });
    await client.PutItemAsync(new PutItemRequest()
    {
      TableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
      Item = item,
    });

    context.Logger.LogInformation("Event created");

    var eventDto = EventMapper.ToDto(item);
    await SaveTags(eventDto, client, context);

    return FunctionHelpers.Respond(
      eventDto,
      CustomJsonSerializerContext.Default.EventDto);
  }

  public static async Task SaveTags(
    EventDto item, AmazonDynamoDBClient client, ILambdaContext context)
  {
    if (item.Tags is null or { Length: 0 })
    {
      return;
    }

    var tableName = Environment.GetEnvironmentVariable("TABLE_NAME")!;
    var config = new DynamoDBOperationConfig()
    {
      OverrideTableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
    };

    context.Logger.LogInformation($"Attempting to save {item.Tags!.Length} tag(s)");
    await client.BatchWriteItemAsync(new BatchWriteItemRequest()
    {
      RequestItems = new()
      {
        [tableName] = item.Tags.SelectMany(value =>
          new List<WriteRequest>
          {
            new(new PutRequest()
            {
              Item = TagMapper.FromDto(new TagDto
              {
                AccountId = item.AccountId,
                Value = value,
              }),
            }),
            new(new PutRequest()
            {
              Item = EventTagMapper.FromDto(new EventTagDto
              {
                AccountId = item.AccountId,
                Date = item.Date,
                Value = value,
              }),
            }),
          }).ToList(),
      },
    });

    context.Logger.LogInformation("Tags saved");
  }
}
