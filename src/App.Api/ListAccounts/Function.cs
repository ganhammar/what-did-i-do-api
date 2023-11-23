using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using App.Api.Shared.Extensions;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;

namespace App.Api.ListAccounts;

public class Function
{
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Function))]
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(APIGatewayProxyRequest))]
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(APIGatewayProxyResponse))]
  static Function()
  {
    // AWSSDKHandler.RegisterXRayForAllServices();
  }

  private static async Task Main()
  {
    Func<APIGatewayProxyRequest, ILambdaContext, Task<APIGatewayProxyResponse>> handler = FunctionHandler;
    await LambdaBootstrapBuilder
      .Create(handler, new SourceGeneratorLambdaJsonSerializer<CustomJsonSerializerContext>(options =>
      {
        options.PropertyNameCaseInsensitive = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
      }))
      .Build()
      .RunAsync();
  }

  public static async Task<APIGatewayProxyResponse> FunctionHandler(
    APIGatewayProxyRequest apiGatewayProxyRequest, ILambdaContext context)
  {
    if (!apiGatewayProxyRequest.HasRequiredScopes("account"))
    {
      return FunctionHelpers.UnauthorizedResponse;
    }

    var client = new AmazonDynamoDBClient();

    var subject = apiGatewayProxyRequest.GetSubject();
    ArgumentNullException.ThrowIfNull(subject, nameof(Member.Subject));

    var tableName = Environment.GetEnvironmentVariable("TABLE_NAME");
    ArgumentNullException.ThrowIfNull(tableName, nameof(tableName));

    var members = await client.QueryAsync(new()
    {
      TableName = tableName,
      IndexName = "Subject-index",
      KeyConditionExpression = "Subject = :subject AND begins_with(PartitionKey, :partitionKey)",
      ExpressionAttributeValues = new Dictionary<string, AttributeValue>
      {
        { ":subject", new AttributeValue(subject) },
        { ":partitionKey", new AttributeValue("MEMBER#") },
      },
    });

    context.Logger.LogInformation($"User is member of {members.Count} accounts");

    if (members.Count == 0)
    {
      return FunctionHelpers.Respond(
        new List<AccountDto>(),
        CustomJsonSerializerContext.Default.ListAccountDto);
    }

    var items = await client.BatchGetItemAsync(new BatchGetItemRequest
    {
      RequestItems = new Dictionary<string, KeysAndAttributes>
      {
        {
          tableName,
          new KeysAndAttributes
          {
            Keys = members.Items.Select(x => MemberMapper.ToDtoDD(x).AccountId).Distinct().Select(accountId =>
            {
              var account = AccountMapper.FromDto(new AccountDto
              {
                Id = accountId,
              });
              return new Dictionary<string, AttributeValue>
              {
                { nameof(account.PartitionKey), new AttributeValue(account.PartitionKey) },
                { nameof(account.SortKey), new AttributeValue(account.SortKey) },
              };
            }).ToList(),
          }
        },
      },
    });

    context.Logger.LogInformation($"Fetched {items.Responses.Count} unique accounts");

    return FunctionHelpers.Respond(
      items.Responses.First().Value.Select(AccountMapper.ToDtoDD).ToList(),
      CustomJsonSerializerContext.Default.ListAccountDto);
  }
}
