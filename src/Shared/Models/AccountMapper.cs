using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using App.Api.Shared.Extensions;

namespace App.Api.Shared.Models;

public static class AccountMapper
{
  public static AccountDto ToDto(Account instance) => new()
  {
    Id = GetId(instance.PartitionKey!),
    CreateDate = instance.CreateDate,
    Name = instance.Name,
  };

  public static Account FromDto(AccountDto instance) => new()
  {
    PartitionKey = GetAccountId(instance.Id!),
    SortKey = GetSortKey(instance),
    CreateDate = instance.CreateDate,
    Name = instance.Name,
  };

  public static AccountDto ToDtoDD(Dictionary<string, AttributeValue> items) => new()
  {
    Id = GetId(items["PartitionKey"].S!),
    CreateDate = DateTime.Parse(items["CreateDate"].S!),
    Name = items["Name"].S!,
  };

  public static Dictionary<string, AttributeValue> FromDtoDD(AccountDto accountDto) => new()
  {
    { "PartitionKey", new AttributeValue(GetAccountId(accountDto.Id!)) },
    { "SortKey", new AttributeValue(GetSortKey(accountDto)) },
    { "CreateDate", new AttributeValue(accountDto.CreateDate.ToString("O")) },
    { "Name", new AttributeValue(accountDto.Name) },
  };

  public static string GetId(string partitionKey)
    => partitionKey.Split('#')[1];

  public static string GetAccountId(string id)
    => $"ACCOUNT#{id}";

  public static string GetSortKey(AccountDto instance) => "#";

  public static async Task<string> GetUniqueId(
    string name, AmazonDynamoDBClient client, CancellationToken cancellationToken = default)
  {
    var baseKey = GetAccountId(name.UrlFriendly());
    var suffix = 0;
    var key = baseKey;
    var exits = true;

    while (exits)
    {
      var search = await client.QueryAsync(new()
      {
        TableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
        KeyConditionExpression = "PartitionKey = :partitionKey",
        ExpressionAttributeValues = new()
        {
          { ":partitionKey", new AttributeValue(key) },
        },
        Limit = 1,
      }, cancellationToken);

      if (search.Count == 1)
      {
        suffix += 1;
        key = $"{baseKey}-{suffix}";
      }
      else
      {
        exits = false;
      }
    }

    return GetId(key);
  }
}
