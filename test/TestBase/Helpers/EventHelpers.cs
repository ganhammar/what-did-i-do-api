using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using App.Api.Shared.Models;

namespace TestBase.Helpers;

public static class EventHelpers
{
  public static Dictionary<string, AttributeValue> CreateEvent(EventDto eventDto)
  {
    var tableName = Environment.GetEnvironmentVariable("TABLE_NAME")!;
    var item = EventMapper.FromDto(eventDto);
    var client = new AmazonDynamoDBClient();
    client.PutItemAsync(new PutItemRequest()
    {
      TableName = tableName,
      Item = item,
    }).GetAwaiter().GetResult();

    if (eventDto.Tags != default && eventDto.Tags.Length > 0)
    {
      client.BatchWriteItemAsync(new BatchWriteItemRequest()
      {
        RequestItems = new()
        {
          {
            tableName,
            eventDto.Tags.Select(tag => new WriteRequest()
            {
              PutRequest = new PutRequest()
              {
                Item = EventTagMapper.FromDto(new()
                {
                  AccountId = eventDto.AccountId,
                  Date = eventDto.Date,
                  Value = tag,
                }),
              },
            }).ToList()
          },
        },
      }).GetAwaiter().GetResult();
    }

    return item;
  }
}
