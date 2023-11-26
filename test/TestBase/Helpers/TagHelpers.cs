using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using App.Api.Shared.Models;

namespace TestBase.Helpers;

public static class TagHelpers
{
  public static void CreateTags(string accountId, params string[] tags)
  {
    var tableName = Environment.GetEnvironmentVariable("TABLE_NAME");
    var client = new AmazonDynamoDBClient();

    client.BatchWriteItemAsync(new BatchWriteItemRequest()
    {
      RequestItems = new()
      {
        [tableName!] = tags.Select(tag => new WriteRequest(new PutRequest()
        {
          Item = TagMapper.FromDto(new()
          {
            AccountId = accountId,
            Value = tag,
          }),
        })).ToList(),
      },
    }).GetAwaiter().GetResult();
  }
}
