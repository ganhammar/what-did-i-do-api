﻿using System.Text.RegularExpressions;
using Amazon.DynamoDBv2.Model;
using App.Api.Shared.Extensions;

namespace App.Api.Shared.Models;

public static partial class MemberMapper
{
  public static MemberDto ToDto(Dictionary<string, AttributeValue> items) => new()
  {
    Id = $"{items["PartitionKey"].S}&{items["SortKey"].S}".To64(),
    AccountId = GetAccountId(items["PartitionKey"].S!),
    Subject = items["Subject"].S!,
    Role = Enum.Parse<Role>(SortKeyRegex().Match(items["SortKey"].S).Groups[1].Value),
    CreateDate = DateTime.Parse(items["CreateDate"].S!),
    Email = items["Email"].S!,
  };

  public static Dictionary<string, AttributeValue> FromDto(MemberDto instance) => new()
  {
    { "PartitionKey", new AttributeValue(instance.Id != default
      ? GetKeys(instance.Id)[0]
      : GetPartitionKey(instance.AccountId!)) },
    { "SortKey", new AttributeValue(instance.Id != default
      ? GetKeys(instance.Id)[1]
      : GetSortKey(instance)) },
    { "CreateDate", new AttributeValue(instance.CreateDate.ToString("O")) },
    { "Subject", new AttributeValue(instance.Subject) },
    { "Email", new AttributeValue(instance.Email) },
  };

  public static string GetAccountId(string partitionKey)
    => partitionKey!.Split("#")[1];

  public static string GetPartitionKey(string accountId)
    => $"MEMBER#{accountId}";

  public static string GetSortKey(MemberDto instance)
    => $"#ROLE#{instance.Role}#USER#{instance.Subject}";

  public static string[] GetKeys(string? id)
  {
    if (id == default)
    {
      return [];
    }

    return id.From64()?.Split('&', 2) ?? [];
  }

  [GeneratedRegex("#ROLE#(.+?)#USER#")]
  private static partial Regex SortKeyRegex();
}
