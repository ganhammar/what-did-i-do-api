using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using App.Api.Shared.Models;

namespace TestBase.Helpers;

public static class AccountHelpers
{
  public static Dictionary<string, AttributeValue> CreateAccount(AccountDto accountDto)
  {
    var tableName = Environment.GetEnvironmentVariable("TABLE_NAME");
    var item = AccountMapper.FromDto(accountDto);
    var client = new AmazonDynamoDBClient();
    var dbContext = new DynamoDBContext(client);
    dbContext.SaveAsync(item, new()
    {
      OverrideTableName = tableName,
    }, CancellationToken.None).GetAwaiter().GetResult();

    return item;
  }

  public static Dictionary<string, AttributeValue> AddOwner(
    Dictionary<string, AttributeValue> account, string subject, string email)
  {
    var accountDto = AccountMapper.ToDto(account);
    var tableName = Environment.GetEnvironmentVariable("TABLE_NAME");
    var item = MemberMapper.FromDto(new()
    {
      AccountId = accountDto.Id,
      Subject = subject,
      Email = email,
      Role = Role.Owner,
      CreateDate = DateTime.UtcNow,
    });
    var client = new AmazonDynamoDBClient();
    client.PutItemAsync(new()
    {
      TableName = tableName,
      Item = item,
    }).GetAwaiter().GetResult();

    while (IsIndexUpdated(client, subject) == false)
    {
      Thread.Sleep(1000);
    }

    return item;
  }

  private static bool IsIndexUpdated(AmazonDynamoDBClient client, string subject)
  {
    var search = client.QueryAsync(new()
    {
      TableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
      IndexName = "Subject-index",
      KeyConditionExpression = "Subject = :subject AND begins_with(PartitionKey, :partitionKey)",
      ExpressionAttributeValues = new()
      {
        { ":subject", new AttributeValue(subject) },
        { ":partitionKey", new AttributeValue("MEMBER#") },
      },
      Limit = 1,
    }).GetAwaiter().GetResult();

    return search.Count > 0;
  }
}
