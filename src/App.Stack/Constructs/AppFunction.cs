using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Constructs;

namespace AppStack.Constructs;

public class AppFunction(Construct scope, string id, AppFunction.Props props)
  : Function(scope, $"{id}Function", new FunctionProps
  {
    Runtime = Runtime.PROVIDED_AL2023,
    Architecture = Architecture.X86_64,
    Handler = props.Handler,
    Code = Code.FromAsset($"./.output/{id}.zip"),
    Timeout = Duration.Minutes(1),
    MemorySize = props.MemorySize,
    LogGroup = new LogGroup(scope, $"{id}LogGroup", new LogGroupProps
    {
      LogGroupName = $"/aws/lambda/{id}",
      Retention = RetentionDays.ONE_DAY,
      RemovalPolicy = RemovalPolicy.DESTROY,
    }),
    Tracing = Tracing.ACTIVE,
    Environment = new Dictionary<string, string>
    {
      { "TABLE_NAME", props.TableName ?? "" },
    },
  })
{
  public class Props(string handler, string? tableName = default, int memorySize = 1769)
  {
    public string Handler { get; set; } = handler;
    public string? TableName { get; set; } = tableName;
    public int MemorySize { get; set; } = memorySize;
  }
}
