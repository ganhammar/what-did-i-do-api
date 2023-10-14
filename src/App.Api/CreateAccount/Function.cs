using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using App.Api.Shared.Extensions;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using AWS.Lambda.Powertools.Logging;

[assembly: LambdaSerializer(typeof(CamelCaseLambdaJsonSerializer))]

namespace App.Api.CreateAccount;

public class Function : FunctionBase
{
  public class Command
  {
    public string? Name { get; set; }
  }

  public Function() : base("account") { }

  protected override async Task<APIGatewayHttpApiV2ProxyResponse> Handler(
    APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    Logger.LogInformation("Attempting to create account");

    var request = TryDeserialize<Command>(apiGatewayProxyRequest);

    if (request == default)
    {
      return Respond(request);
    }

    var client = new DynamoDBContext(new AmazonDynamoDBClient());
    var id = await AccountMapper.GetUniqueId(request.Name!, client);
    Logger.LogInformation($"The unique Id for the account is {id}");

    var config = new DynamoDBOperationConfig
    {
      OverrideTableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
    };

    var item = AccountMapper.FromDto(new AccountDto
    {
      Id = id,
      Name = request.Name,
      CreateDate = DateTime.UtcNow,
    });
    await client.SaveAsync(item, config);
    Logger.LogInformation("Account created");

    var member = MemberMapper.FromDto(new MemberDto
    {
      AccountId = id,
      Role = Role.Owner,
      Subject = apiGatewayProxyRequest.GetSubject(),
      Email = apiGatewayProxyRequest.GetEmail(),
      CreateDate = DateTime.UtcNow,
    });

    ArgumentNullException.ThrowIfNull(member.Subject, nameof(member.Subject));
    ArgumentNullException.ThrowIfNull(member.Email, nameof(member.Email));

    Logger.LogInformation("Attempting to create account member of type owner");
    await client.SaveAsync(member, config);

    Logger.LogInformation("Member created");
    return Respond(AccountMapper.ToDto(item));
  }
}
