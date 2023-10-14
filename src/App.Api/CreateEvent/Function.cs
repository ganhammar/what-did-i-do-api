using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using AWS.Lambda.Powertools.Logging;

[assembly: LambdaSerializer(typeof(CamelCaseLambdaJsonSerializer))]

namespace App.Api.CreateEvent;

public class Function : FunctionBase
{
  public class Command
  {
    public string? AccountId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? Date { get; set; }
    public string[]? Tags { get; set; }
  }

  public Function() : base("event") { }

  protected override async Task<APIGatewayHttpApiV2ProxyResponse> Handler(
    APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    Logger.LogInformation("Attempting to create Event");

    var request = TryDeserialize<Command>(apiGatewayProxyRequest);

    if (request == default)
    {
      return Respond(request);
    }

    var client = new DynamoDBContext(new AmazonDynamoDBClient());
    var item = EventMapper.FromDto(new EventDto
    {
      AccountId = request.AccountId,
      Title = request.Title,
      Description = request.Description,
      Date = request.Date?.ToUniversalTime() ?? DateTime.UtcNow,
      Tags = request.Tags?.Distinct().ToArray(),
    });
    await client.SaveAsync(item, new()
    {
      OverrideTableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
    }, default);

    Logger.LogInformation("Event created");

    var eventDto = EventMapper.ToDto(item);
    await SaveTags(eventDto, client);

    return Respond(eventDto);
  }

  public async Task SaveTags(EventDto item, DynamoDBContext client)
  {
    if (item.Tags?.Any() != true)
    {
      return;
    }

    var config = new DynamoDBOperationConfig()
    {
      OverrideTableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
    };

    Logger.LogInformation($"Attempting to save {item.Tags.Count()} tag(s)");
    var tags = client.CreateBatchWrite<Tag>(config);
    var eventTags = client.CreateBatchWrite<EventTag>(config);

    foreach (var value in item.Tags.Distinct())
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

    await tags.ExecuteAsync();
    await eventTags.ExecuteAsync();
    Logger.LogInformation("Tags saved");
  }
}
