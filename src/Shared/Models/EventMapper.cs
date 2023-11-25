using Amazon.DynamoDBv2.Model;
using App.Api.Shared.Extensions;

namespace App.Api.Shared.Models;

public static class EventMapper
{
  public static EventDto ToDtoDD(Dictionary<string, AttributeValue> instance) => new()
  {
    Id = $"{instance["PartitionKey"].S}&{instance["SortKey"].S}".To64(),
    AccountId = GetAccountId(instance["PartitionKey"].S),
    Date = DateTime.Parse(instance["SortKey"].S).ToUniversalTime(),
    Title = instance["Title"].S,
    Description = instance.TryGetValue("Description", out var description)
      ? description.S
      : default,
    Tags = instance.TryGetValue("Tags", out var tags)
      ? tags.SS.ToArray()
      : default,
  };

  public static EventDto ToDto(Event instance) => new()
  {
    Id = $"{instance.PartitionKey}&{instance.SortKey}".To64(),
    AccountId = GetAccountId(instance.PartitionKey!),
    Date = instance.Date,
    Title = instance.Title,
    Description = instance.Description,
    Tags = instance.Tags,
  };

  public static Dictionary<string, AttributeValue> FromDtoDD(EventDto instance)
  {
    var items = new Dictionary<string, AttributeValue>()
    {
      { "PartitionKey", new AttributeValue(instance.Id != default
        ? GetKeys(instance.Id)[0]
        : GetPartitionKey(instance.AccountId!)) },
      { "SortKey", new AttributeValue(instance.Id != default
        ? GetKeys(instance.Id)[1]
        : instance.Date.ToUniversalString()) },
      { "Title", new AttributeValue(instance.Title) },
    };

    if (instance.Description != default)
    {
      items.Add("Description", new AttributeValue(instance.Description));
    }

    if (instance.Tags != default)
    {
      items.Add("Tags", new AttributeValue(instance.Tags.ToList()));
    }

    return items;
  }

  public static Event FromDto(EventDto instance) => new()
  {
    PartitionKey = instance.Id != default
      ? GetKeys(instance.Id)[0]
      : GetPartitionKey(instance.AccountId!),
    SortKey = instance.Id != default
      ? GetKeys(instance.Id)[1]
      : instance.Date.ToUniversalString(),
    Date = instance.Date,
    Title = instance.Title,
    Description = instance.Description,
    Tags = instance.Tags,
  };

  public static string GetAccountId(string partitionKey)
    => partitionKey!.Split("#")[2];

  public static string GetPartitionKey(string accountId)
    => $"EVENT#ACCOUNT#{accountId}";

  public static string[] GetKeys(string? id)
  {
    if (id == default)
    {
      return [];
    }

    return id.From64()?.Split('&', 2) ?? [];
  }
}
