using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
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

namespace App.Api.ListEvents;

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

  private static readonly IAmazonDynamoDB Client = new AmazonDynamoDBClient();

  public static async Task<APIGatewayProxyResponse> FunctionHandler(
    APIGatewayProxyRequest apiGatewayProxyRequest, ILambdaContext context)
  {
    if (!apiGatewayProxyRequest.HasRequiredScopes("event"))
    {
      return FunctionHelpers.UnauthorizedResponse;
    }

    var request = ParseRequest(apiGatewayProxyRequest);

    var validationResult = ValidateRequest(request);

    if (validationResult is not null and { Length: > 0 })
    {
      return FunctionHelpers.Respond(validationResult, CustomJsonSerializerContext.Default.FunctionErrorArray, false);
    }

    var fromDate = request.FromDate ?? DateTime.UtcNow.Date;
    var toDate = request.ToDate ?? DateTime.UtcNow.AddDays(1).Date;

    if (request.Tag != default)
    {
      return await ListEventsByTag(request, fromDate, toDate);
    }

    return await ListEvents(request, fromDate, toDate);
  }

  private static ListEventsInput ParseRequest(APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    var queryStringParameters = new Dictionary<string, string>(
      apiGatewayProxyRequest.QueryStringParameters ?? new Dictionary<string, string>(),
      StringComparer.OrdinalIgnoreCase);

    queryStringParameters.TryGetValue("accountid", out var accountId);
    queryStringParameters.TryGetValue("fromdate", out var fromDateRaw);
    queryStringParameters.TryGetValue("todate", out var toDateRaw);
    queryStringParameters.TryGetValue("limit", out var limitRaw);
    queryStringParameters.TryGetValue("tag", out var tag);
    queryStringParameters.TryGetValue("paginationtoken", out var paginationToken);

    var fromDate = TryParseDateTime(fromDateRaw);
    var toDate = TryParseDateTime(toDateRaw);
    _ = int.TryParse(limitRaw, out var limit);

    return new ListEventsInput
    {
      AccountId = accountId,
      FromDate = fromDate,
      ToDate = toDate,
      Limit = limit,
      Tag = tag,
      PaginationToken = paginationToken,
    };
  }

  private static FunctionError[] ValidateRequest(ListEventsInput request)
  {
    var result = new List<FunctionError>();

    if (request.Limit is < 1 or > 200)
    {
      result.Add(new(nameof(ListEventsInput.Limit), "Limit must be greater than zero and less than 200")
      {
        ErrorCode = "InvalidInput",
      });
    }

    if (request.ToDate is not null && request.FromDate is null)
    {
      result.Add(new(nameof(ListEventsInput.FromDate), "FromDate must have a value if ToDate is set")
      {
        ErrorCode = "NotEmpty",
      });
    }

    if (request.FromDate is not null && request.ToDate is null)
    {
      result.Add(new(nameof(ListEventsInput.ToDate), "ToDate must have a value if FromDate is set")
      {
        ErrorCode = "NotEmpty",
      });
    }

    if (request.ToDate < request.FromDate)
    {
      result.Add(new(nameof(ListEventsInput.ToDate), "ToDate cannot be less than FromDate")
      {
        ErrorCode = "InvalidInput",
      });
    }

    return [.. result];
  }

  private static DateTime? TryParseDateTime(string? date)
  {
    if (DateTime.TryParse(date, out var parseDate))
    {
      return parseDate;
    }

    return default;
  }

  private static async Task<APIGatewayProxyResponse> ListEventsByTag(
    ListEventsInput request, DateTime fromDate, DateTime toDate)
  {
    Logger.LogInformation($"Listing Events with the tag {request.Tag} between {fromDate:o} and {toDate:o} for account {request.AccountId}");

    var tableName = Environment.GetEnvironmentVariable("TABLE_NAME")!;
    var query = await Client.QueryAsync(new()
    {
      TableName = tableName,
      KeyConditionExpression = "PartitionKey = :partitionKey AND SortKey BETWEEN :fromDate AND :toDate",
      ExpressionAttributeValues = new Dictionary<string, AttributeValue>
      {
        { ":partitionKey", new(EventTagMapper.GetPartitionKey(request.AccountId!)) },
        { ":fromDate", new(EventTagMapper.GetSortKey(request.Tag, fromDate)) },
        { ":toDate", new(EventTagMapper.GetSortKey(request.Tag, toDate)) },
      },
      Limit = request.Limit,
      ScanIndexForward = false,
      ExclusiveStartKey = FromBase64(request.PaginationToken),
    });

    Logger.LogInformation($"Found {query.Items.Count} Event(s) with matching tag");

    var eventTags = query.Items.Select(EventTagMapper.ToDtoDD);

    var batch = await Client.BatchGetItemAsync(new BatchGetItemRequest()
    {
      RequestItems = new Dictionary<string, KeysAndAttributes>
      {
        {
          tableName,
          new()
          {
            Keys = eventTags.Select(x =>
            {
              var item = EventMapper.FromDto(new()
              {
                AccountId = x.AccountId,
                Date = x.Date,
              });
              return new Dictionary<string, AttributeValue>
              {
                { "PartitionKey", new(item.PartitionKey) },
                { "SortKey", new(item.SortKey) },
              };
            }).ToList(),
          }
        },
      },
    });

    return FunctionHelpers.Respond(new ListEventsResult()
    {
      PaginationToken = ToBase64(query.LastEvaluatedKey),
      Items = batch.Responses.First().Value.Select(EventMapper.ToDtoDD).ToList(),
    }, CustomJsonSerializerContext.Default.ListEventsResult);
  }

  private static async Task<APIGatewayProxyResponse> ListEvents(
    ListEventsInput request, DateTime fromDate, DateTime toDate)
  {
    Logger.LogInformation($"Listing Events between {fromDate:o} and {toDate:o} for account {request.AccountId}");

    var query = await Client.QueryAsync(new()
    {
      KeyConditionExpression = "PartitionKey = :partitionKey AND SortKey BETWEEN :fromDate AND :toDate",
      ExpressionAttributeValues = new Dictionary<string, AttributeValue>
      {
        { ":partitionKey", new(EventMapper.GetPartitionKey(request.AccountId!)) },
        { ":fromDate", new(fromDate.ToUniversalString()) },
        { ":toDate", new(toDate.ToUniversalString()) },
      },
      Limit = request.Limit,
      ScanIndexForward = false,
      TableName = Environment.GetEnvironmentVariable("TABLE_NAME"),
      ExclusiveStartKey = FromBase64(request.PaginationToken),
    });

    var events = query.Items.Select(EventMapper.ToDtoDD).ToList();

    Logger.LogInformation($"Found {events.Count} Event(s)");

    return FunctionHelpers.Respond(new ListEventsResult()
    {
      PaginationToken = ToBase64(query.LastEvaluatedKey),
      Items = events,
    }, CustomJsonSerializerContext.Default.ListEventsResult);
  }

  private static string? ToBase64(Dictionary<string, AttributeValue> dictionary)
  {
    if (dictionary.Count == 0)
    {
      return default;
    }

    var json = JsonSerializer.Serialize(
      dictionary,
      CustomJsonSerializerContext.Default.DictionaryStringAttributeValue);
    return Convert.ToBase64String(Encoding.UTF8.GetBytes(json.ToString()));
  }

  private static Dictionary<string, AttributeValue>? FromBase64(string? token)
  {
    if (token == default)
    {
      return default;
    }

    var bytes = Convert.FromBase64String(token);
    var deserializedToken = JsonSerializer.Deserialize(
      Encoding.UTF8.GetString(bytes),
      CustomJsonSerializerContext.Default.DictionaryStringAttributeValue);

    if (deserializedToken is null)
    {
      return default;
    }

    foreach (KeyValuePair<string, AttributeValue> item in deserializedToken)
    {
      SetPrivatePropertyValue<bool?>(item.Value, "_null", null);
    }

    return deserializedToken;
  }

  public static void SetPrivatePropertyValue<T>(object obj, string propName, T val)
  {
    foreach (var fi in obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
    {
      if (fi.Name.Contains(propName, StringComparison.CurrentCultureIgnoreCase))
      {
        fi.SetValue(obj, val);
        break;
      }
    }
  }
}
