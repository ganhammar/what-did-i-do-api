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
using AWS.Lambda.Powertools.Logging;

namespace App.Api.ListTags;

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
    if (!apiGatewayProxyRequest.HasRequiredScopes("event"))
    {
      return FunctionHelpers.UnauthorizedResponse;
    }

    var queryStringParameters = new Dictionary<string, string>(
      apiGatewayProxyRequest.QueryStringParameters ?? new Dictionary<string, string>(),
      StringComparer.OrdinalIgnoreCase);

    queryStringParameters.TryGetValue("accountid", out var accountId);

    Logger.LogInformation($"Listing tags for account {accountId}");

    var client = new AmazonDynamoDBClient();
    var tags = await client.QueryAsync(new()
    {
      TableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
      KeyConditionExpression = "PartitionKey = :partitionKey",
      ExpressionAttributeValues = new()
      {
        { ":partitionKey", new AttributeValue(TagMapper.GetPartitionKey(accountId!)) },
      },
      ScanIndexForward = false,
    });

    context.Logger.LogInformation($"Found {tags.Count} tag(s)");

    return FunctionHelpers.Respond(
      tags.Items.Select(TagMapper.ToDto).ToList(),
      CustomJsonSerializerContext.Default.ListTagDto);
  }
}
