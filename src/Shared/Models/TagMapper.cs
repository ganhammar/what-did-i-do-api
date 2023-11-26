using Amazon.DynamoDBv2.Model;

namespace App.Api.Shared.Models;

public static class TagMapper
{
  public static TagDto ToDto(Dictionary<string, AttributeValue> instance) => new()
  {
    AccountId = GetAccountId(instance["PartitionKey"].S),
    Value = instance["SortKey"].S,
  };

  public static Dictionary<string, AttributeValue> FromDto(TagDto instance) => new()
  {
    { "PartitionKey", new AttributeValue(GetPartitionKey(instance.AccountId!)) },
    { "SortKey", new AttributeValue(instance.Value) },
  };

  public static string GetAccountId(string partitionKey)
    => partitionKey!.Split("#")[2];

  public static string GetPartitionKey(string accountId)
    => $"TAG#ACCOUNT#{accountId}";
}
