using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using App.Api.Shared.Models;

namespace App.Api.Shared.Infrastructure;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(APIGatewayProxyRequest))]
[JsonSerializable(typeof(APIGatewayProxyResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(FunctionError[]))]
[JsonSerializable(typeof(CreateAccountInput))]
[JsonSerializable(typeof(AccountDto))]
[JsonSerializable(typeof(List<AccountDto>))]
public partial class CustomJsonSerializerContext : JsonSerializerContext
{
}
