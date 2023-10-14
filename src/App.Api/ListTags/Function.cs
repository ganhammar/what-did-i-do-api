using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using AWS.Lambda.Powertools.Logging;

[assembly: LambdaSerializer(typeof(CamelCaseLambdaJsonSerializer))]

namespace App.Api.ListTags;

public class Function : FunctionBase
{
  public Function() : base("event") { }

  protected override async Task<APIGatewayHttpApiV2ProxyResponse> Handler(
    APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    var queryStringParameters = new Dictionary<string, string>(
      apiGatewayProxyRequest.QueryStringParameters ?? new Dictionary<string, string>(),
      StringComparer.OrdinalIgnoreCase);

    queryStringParameters.TryGetValue("accountid", out var accountId);

    Logger.LogInformation($"Listing tags for account {accountId}");

    var client = new DynamoDBContext(new AmazonDynamoDBClient());
    var search = client.FromQueryAsync<Tag>(
      new()
      {
        KeyExpression = new Expression
        {
          ExpressionStatement = "PartitionKey = :partitionKey",
          ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
          {
            { ":partitionKey", TagMapper.GetPartitionKey(accountId!) },
          },
        },
        BackwardSearch = true,
      },
      new()
      {
        OverrideTableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
      });
    var tags = await search.GetRemainingAsync();

    Logger.LogInformation($"Found {tags.Count} tag(s)");

    return Respond(tags.Select(x => TagMapper.ToDto(x)).ToList());
  }
}
