using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using AWS.Lambda.Powertools.Logging;

[assembly: LambdaSerializer(typeof(CamelCaseLambdaJsonSerializer))]

namespace App.Api.EditEvent;

public class Function : FunctionBase
{
  public class Command
  {
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string[]? Tags { get; set; }
  }

  public Function() : base("event") { }

  protected override async Task<APIGatewayHttpApiV2ProxyResponse> Handler(
    APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    var request = TryDeserialize<Command>(apiGatewayProxyRequest);

    if (request == default)
    {
      return Respond(request);
    }

    Logger.LogInformation("Attempting to edit Event");

    var client = new DynamoDBContext(new AmazonDynamoDBClient());
    var config = new DynamoDBOperationConfig()
    {
      OverrideTableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
    };
    var item = EventMapper.FromDto(new EventDto
    {
      Id = request.Id,
    });
    item = await client.LoadAsync<Event>(
      item.PartitionKey, item.SortKey, config);

    var eventDto = EventMapper.ToDto(item);
    await SaveTags(eventDto, request.Tags, client);

    item.Title = request.Title;
    item.Description = request.Description;
    item.Tags = request.Tags;

    await client.SaveAsync(item, config);

    Logger.LogInformation("Event editd");

    eventDto = EventMapper.ToDto(item);

    return Respond(eventDto);
  }

  public async Task SaveTags(
    EventDto item, string[]? newTags, DynamoDBContext client)
  {
    if (item.Tags?.Any() != true && newTags?.Any() != true)
    {
      return;
    }

    var config = new DynamoDBOperationConfig()
    {
      OverrideTableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
    };

    Logger.LogInformation($"Attempting to update tag(s)");

    var tags = client.CreateBatchWrite<Tag>(config);
    var eventTags = client.CreateBatchWrite<EventTag>(config);

    // Delete old tags that no longer applies
    if (item.Tags?.Any() == true)
    {
      foreach (var value in item.Tags)
      {
        if (newTags?.Any() == false || newTags!.Contains(value) == false)
        {
          eventTags.AddDeleteItem(EventTagMapper.FromDto(new EventTagDto
          {
            AccountId = item.AccountId,
            Date = item.Date,
            Value = value,
          }));
        }
      }
    }

    // Create or update
    if (newTags?.Any() == true)
    {
      foreach (var value in newTags.Distinct())
      {
        tags.AddPutItem(TagMapper.FromDto(new TagDto
        {
          AccountId = item.AccountId,
          Value = value,
        }));

        eventTags.AddPutItem(EventTagMapper.FromDto(new EventTagDto
        {
          AccountId = item.AccountId,
          Date = item.Date,
          Value = value,
        }));
      }
    }

    await tags.ExecuteAsync();
    await eventTags.ExecuteAsync();
    Logger.LogInformation("Tags saved");
  }
}
