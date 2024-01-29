using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using App.Api.Shared.Extensions;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;

namespace App.Api.CreateAccount;

public class Function
{
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Function))]
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(APIGatewayProxyRequest))]
  [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(APIGatewayProxyResponse))]
  static Function()
  { }

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
    else if (string.IsNullOrEmpty(apiGatewayProxyRequest.Body))
    {
      return FunctionHelpers.NoBodyResponse;
    }

    var request = JsonSerializer.Deserialize(
      apiGatewayProxyRequest.Body,
      CustomJsonSerializerContext.Default.CreateAccountInput);

    if (request is null)
    {
      return FunctionHelpers.NoBodyResponse;
    }

    var client = new AmazonDynamoDBClient();
    var id = await AccountMapper.GetUniqueId(request.Name!, client);
    context.Logger.LogInformation($"Creating account with id \"{id}\"");

    var tableName = Environment.GetEnvironmentVariable("TABLE_NAME");

    var item = AccountMapper.FromDto(new AccountDto
    {
      Id = id,
      Name = request.Name,
      CreateDate = DateTime.UtcNow,
    });
    await client.PutItemAsync(tableName, item);

    var subject = apiGatewayProxyRequest.GetSubject();
    var email = apiGatewayProxyRequest.GetEmail();

    ArgumentNullException.ThrowIfNull(subject, nameof(MemberDto.Subject));
    ArgumentNullException.ThrowIfNull(email, nameof(MemberDto.Email));

    var member = MemberMapper.FromDto(new MemberDto
    {
      AccountId = id,
      Role = Role.Owner,
      Subject = apiGatewayProxyRequest.GetSubject(),
      Email = apiGatewayProxyRequest.GetEmail(),
      CreateDate = DateTime.UtcNow,
    });

    await client.PutItemAsync(tableName, member);

    return FunctionHelpers.Respond(AccountMapper.ToDto(item), CustomJsonSerializerContext.Default.AccountDto);
  }
}
