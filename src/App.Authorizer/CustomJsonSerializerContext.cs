using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;

namespace App.Authorizer;

[JsonSerializable(typeof(APIGatewayCustomAuthorizerRequest))]
[JsonSerializable(typeof(APIGatewayCustomAuthorizerResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class CustomJsonSerializerContext : JsonSerializerContext
{
}
