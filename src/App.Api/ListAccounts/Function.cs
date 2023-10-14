using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using App.Api.Shared.Extensions;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using AWS.Lambda.Powertools.Logging;

[assembly: LambdaSerializer(typeof(CamelCaseLambdaJsonSerializer))]

namespace App.Api.ListAccounts;

public class Function : FunctionBase
{
  public Function() : base("account") { }

  protected override async Task<APIGatewayHttpApiV2ProxyResponse> Handler(
    APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    Logger.LogInformation("Listing accounts for logged in user");

    var client = new DynamoDBContext(new AmazonDynamoDBClient());
    var subject = apiGatewayProxyRequest.GetSubject();

    ArgumentNullException.ThrowIfNull(subject, nameof(Member.Subject));

    var config = new DynamoDBOperationConfig
    {
      OverrideTableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
    };
    var search = client.FromQueryAsync<Member>(new()
    {
      IndexName = "Subject-index",
      KeyExpression = new Expression
      {
        ExpressionStatement = "Subject = :subject AND begins_with(PartitionKey, :partitionKey)",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":subject", subject },
          { ":partitionKey", "MEMBER#" },
        }
      },
    }, config);
    var memberAccounts = await search.GetRemainingAsync();

    Logger.LogInformation($"User is member of {memberAccounts.Count} accounts");

    var batch = client.CreateBatchGet<Account>(config);
    foreach (var accountId in memberAccounts.Select(x => MemberMapper.ToDto(x).AccountId).Distinct())
    {
      var account = AccountMapper.FromDto(new AccountDto
      {
        Id = accountId,
      });
      batch.AddKey(account.PartitionKey, account.SortKey);
    }
    await batch.ExecuteAsync();

    Logger.LogInformation($"Fetched {batch.Results.Count} unique accounts");

    return Respond(batch.Results.Select(x => AccountMapper.ToDto(x)).ToList());
  }
}
