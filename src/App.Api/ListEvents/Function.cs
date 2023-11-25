using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using App.Api.Shared.Extensions;
using App.Api.Shared.Infrastructure;
using App.Api.Shared.Models;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Tracing;

[assembly: LambdaSerializer(typeof(CamelCaseLambdaJsonSerializer))]

namespace App.Api.ListEvents;

public class Function : FunctionBase
{
  public class Query
  {
    public string? AccountId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Limit { get; set; }
    public string? Tag { get; set; }
    public string? PaginationToken { get; set; }
  }

  private readonly JsonSerializerOptions _serializerOptions = new()
  {
    Converters =
    {
      new AttributeValueJsonConverter(),
    },
  };
  private readonly DynamoDBContext _client;
  private readonly IAmazonDynamoDB _database;

  public Function()
    : base("event")
  {
    _database = new AmazonDynamoDBClient();
    _client = new DynamoDBContext(_database);
  }

  protected override async Task<APIGatewayHttpApiV2ProxyResponse> Handler(
    APIGatewayProxyRequest apiGatewayProxyRequest)
  {
    var request = ParseRequest(apiGatewayProxyRequest);

    var validationResult = ValidateRequest(request);

    if (validationResult is not null and { Count: > 0 })
    {
      return Respond(validationResult, false);
    }

    var fromDate = request.FromDate ?? DateTime.UtcNow.Date;
    var toDate = request.ToDate ?? DateTime.UtcNow.AddDays(1).Date;

    if (request.Tag != default)
    {
      return await ListEventsByTag(request, fromDate, toDate);
    }

    return await ListEvents(request, fromDate, toDate);
  }

  private Query ParseRequest(APIGatewayProxyRequest apiGatewayProxyRequest)
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
    int.TryParse(limitRaw, out var limit);

    return new Query
    {
      AccountId = accountId,
      FromDate = fromDate,
      ToDate = toDate,
      Limit = limit,
      Tag = tag,
      PaginationToken = paginationToken,
    };
  }

  private static List<FunctionError> ValidateRequest(Query request)
  {
    var result = new List<FunctionError>();

    if (request.Limit is < 1 or > 200)
    {
      result.Add(new(nameof(Query.Limit), "Limit must be greater than zero and less than 200")
      {
        ErrorCode = "InvalidInput",
      });
    }

    if (request.ToDate is not null && request.FromDate is null)
    {
      result.Add(new(nameof(Query.FromDate), "FromDate must have a value if ToDate is set")
      {
        ErrorCode = "NotEmpty",
      });
    }

    if (request.FromDate is not null && request.ToDate is null)
    {
      result.Add(new(nameof(Query.ToDate), "ToDate must have a value if FromDate is set")
      {
        ErrorCode = "NotEmpty",
      });
    }

    if (request.ToDate < request.FromDate)
    {
      result.Add(new(nameof(Query.ToDate), "ToDate cannot be less than FromDate")
      {
        ErrorCode = "InvalidInput",
      });
    }

    return result;
  }

  private static DateTime? TryParseDateTime(string? date)
  {
    if (DateTime.TryParse(date, out var parseDate))
    {
      return parseDate;
    }

    return default;
  }

  [Tracing(SegmentName = "List events by tag")]
  private async Task<APIGatewayHttpApiV2ProxyResponse> ListEventsByTag(
    Query request, DateTime fromDate, DateTime toDate)
  {
    Logger.LogInformation($"Listing Events with the tag {request.Tag} between {fromDate:o} and {toDate:o} for account {request.AccountId}");

    var tableName = Environment.GetEnvironmentVariable("TABLE_NAME");
    var query = await _database.QueryAsync(new()
    {
      KeyConditionExpression = "PartitionKey = :partitionKey AND SortKey BETWEEN :fromDate AND :toDate",
      ExpressionAttributeValues = new Dictionary<string, AttributeValue>
      {
        { ":partitionKey", new(EventTagMapper.GetPartitionKey(request.AccountId)) },
        { ":fromDate", new(EventTagMapper.GetSortKey(request.Tag, fromDate)) },
        { ":toDate", new(EventTagMapper.GetSortKey(request.Tag, toDate)) },
      },
      Limit = request.Limit,
      ScanIndexForward = false,
      TableName = tableName,
      ExclusiveStartKey = FromBase64(request.PaginationToken),
    });

    Logger.LogInformation($"Found {query.Items.Count()} Event(s) with matching tag");

    var eventTags = query.Items.Select(x =>
    {
      var document = Document.FromAttributeMap(x);
      return EventTagMapper.ToDto(_client.FromDocument<EventTag>(document));
    });

    var batch = _client.CreateBatchGet<Event>(new()
    {
      OverrideTableName = tableName,
    });

    foreach (var eventTag in eventTags)
    {
      var item = EventMapper.FromDto(new()
      {
        AccountId = eventTag.AccountId,
        Date = eventTag.Date,
      });
      batch.AddKey(item.PartitionKey, item.SortKey);
    }

    await batch.ExecuteAsync();

    return Respond(new Result()
    {
      Items = batch.Results.Select(x => EventMapper.ToDto(x)).ToList(),
      PaginationToken = ToBase64(query.LastEvaluatedKey),
    });
  }

  [Tracing(SegmentName = "List events")]
  private async Task<APIGatewayHttpApiV2ProxyResponse> ListEvents(
    Query request, DateTime fromDate, DateTime toDate)
  {
    Logger.LogInformation($"Listing Events between {fromDate:o} and {toDate:o} for account {request.AccountId}");

    var query = await _database.QueryAsync(new()
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

    var events = query.Items.Select(x =>
    {
      var document = Document.FromAttributeMap(x);
      return _client.FromDocument<Event>(document);
    });

    Logger.LogInformation($"Found {events.Count()} Event(s)");

    return Respond(new Result()
    {
      PaginationToken = ToBase64(query.LastEvaluatedKey),
      Items = events.Select(x => EventMapper.ToDto(x)).ToList(),
    });
  }

  private string? ToBase64(Dictionary<string, AttributeValue> dictionary)
  {
    if (dictionary.Count == 0)
    {
      return default;
    }

    var json = JsonSerializer.Serialize(dictionary, _serializerOptions);
    return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
  }

  private Dictionary<string, AttributeValue>? FromBase64(string? token)
  {
    if (token == default)
    {
      return default;
    }

    var bytes = Convert.FromBase64String(token);
    return JsonSerializer.Deserialize<Dictionary<string, AttributeValue>>(
      Encoding.UTF8.GetString(bytes), _serializerOptions);
  }
}
