using Amazon.DynamoDBv2.Model;
using App.Api.Shared.Extensions;

namespace App.Api.Shared.Models;

public static class EventTagMapper
{
  public static EventTagDto ToDto(Dictionary<string, AttributeValue> instance) => new()
  {
    AccountId = GetAccountId(instance["PartitionKey"].S),
    Value = GetValue(instance["SortKey"].S),
    Date = GetDate(instance["SortKey"].S),
  };

  public static Dictionary<string, AttributeValue> FromDto(EventTagDto instance) => new()
  {
    { "PartitionKey", new AttributeValue(GetPartitionKey(instance.AccountId)) },
    { "SortKey", new AttributeValue(GetSortKey(instance.Value, instance.Date)) },
  };

  public static string GetAccountId(string partitionKey)
    => partitionKey!.Split("#")[2];

  public static string GetValue(string sortKey)
    => sortKey!.Split("#")[2];

  public static DateTime? GetDate(string sortKey)
  {
    var rawDate = sortKey!.Split("#")[4];
    _ = DateTime.TryParse(rawDate, out var date);

    return date;
  }

  public static string GetPartitionKey(string? accountId)
    => $"EVENT_TAG#ACCOUNT#{accountId}";

  public static string GetSortKey(string? value, DateTime? date)
    => $"#TAG#{value}#DATE#{date.ToUniversalString()}";
}
