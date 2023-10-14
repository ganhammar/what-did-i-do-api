using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using AWS.Lambda.Powertools.Logging;

[assembly: LambdaSerializer(typeof(CamelCaseLambdaJsonSerializer))]

namespace App.Api.DeleteEvent;

public class Function : FunctionBase
{
  public Function() : base("event") { }

  protected override async Task<APIGatewayHttpApiV2ProxyResponse> Handler(
    APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    var queryStringParameters = new Dictionary<string, string>(
      apiGatewayProxyRequest.QueryStringParameters ?? new Dictionary<string, string>(),
      StringComparer.OrdinalIgnoreCase);

    queryStringParameters.TryGetValue("id", out var id);

    if (EventMapper.GetKeys(id).Length != 2)
    {
      return Respond(false);
    }

    Logger.LogInformation("Attempting to delete Event");

    var config = new DynamoDBOperationConfig()
    {
      OverrideTableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
    };
    var client = new DynamoDBContext(new AmazonDynamoDBClient());
    var keys = EventMapper.GetKeys(id);
    var item = await client.LoadAsync<Event>(keys[0], keys[1], config);

    if (item != default)
    {
      Logger.LogInformation("Matching Event found, deleting");
      await client.DeleteAsync(item, config);
      await DeleteEventTags(item, client, config);
    }

    return Respond();
  }

  public async Task DeleteEventTags(
    Event item, DynamoDBContext client, DynamoDBOperationConfig config)
  {
    if (item.Tags?.Any() != true)
    {
      return;
    }

    var eventDto = EventMapper.ToDto(item);
    var batch = client.CreateBatchWrite<EventTag>(config);

    foreach (var tag in eventDto.Tags!)
    {
      batch.AddDeleteItem(EventTagMapper.FromDto(new()
      {
        AccountId = eventDto.AccountId,
        Date = eventDto.Date,
        Value = tag,
      }));
    }

    await batch.ExecuteAsync();
  }
}
