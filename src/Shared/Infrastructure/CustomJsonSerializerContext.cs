using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.Model;
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
[JsonSerializable(typeof(List<TagDto>))]
[JsonSerializable(typeof(EventDto))]
[JsonSerializable(typeof(CreateEventInput))]
[JsonSerializable(typeof(EditEventInput))]
[JsonSerializable(typeof(ListEventsResult))]
[JsonSerializable(typeof(Dictionary<string, AttributeValue>))]
public partial class CustomJsonSerializerContext : JsonSerializerContext
{
}
