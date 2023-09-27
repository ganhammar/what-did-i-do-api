using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.SSM;
using AppStack.Constructs;
using AppStack.Models;
using Constructs;
using Microsoft.Extensions.Configuration;

namespace AppStack;

public class AppStack : Stack
{
  private const string _tableName = "what-did-i-do";
  private readonly IConfiguration _configuration;

  internal AppStack(Construct scope, string id, IStackProps props, IConfiguration configuration)
    : base(scope, id, props)
  {
    _configuration = configuration;

    // DynamoDB
    var applicationTable = Table.FromTableAttributes(this, "ApplicationTable", new TableAttributes
    {
      TableArn = $"arn:aws:dynamodb:{Region}:{Account}:table/{_tableName}",
      GrantIndexPermissions = true,
    });

    // API Gateway
    var api = new RestApi(this, "what-did-i-do-api", new RestApiProps
    {
      RestApiName = "what-did-i-do-api",
      DefaultCorsPreflightOptions = new CorsOptions
      {
        AllowOrigins = new[]
        {
          "http://localhost:3000",
        },
      },
    });
    var apiResource = api.Root.AddResource("api");

    // Authorizer
    var authorizer = CreateAuthorizerFunction();

    // Resource: Account
    var accountResource = apiResource.AddResource("account");
    HandleAccountResource(accountResource, api, applicationTable, authorizer);

    // Resource: Event
    var eventResource = apiResource.AddResource("event");
    HandleEventResource(eventResource, applicationTable, authorizer);

    // Resource: Tag
    var tagResource = apiResource.AddResource("tag");
    HandleTagResource(tagResource, applicationTable, authorizer);

    // Output
    new CfnOutput(this, "APIGWEndpoint", new CfnOutputProps
    {
      Value = api.Url,
    });
  }

  private RequestAuthorizer CreateAuthorizerFunction()
  {
    new StringParameter(this, "AuthorizerClientSecretParameter", new StringParameterProps
    {
      ParameterName = "/WDID/Authorizer/AuthorizationOptions/ClientSecret",
      StringValue = _configuration.GetSection("Authorizer").GetValue<string>("ClientSecret")!,
      Tier = ParameterTier.STANDARD,
    });

    var authorizerFunction = new AppFunction(this, "App.Authorizer", new AppFunction.Props(
      "App.Authorizer::App.Authorizer.Function::FunctionHandler"
    ));

    AllowSsm(authorizerFunction, "/WDID/Authorizer*", false);

    return new RequestAuthorizer(this, "ApiAuthorizer", new RequestAuthorizerProps
    {
      Handler = authorizerFunction,
      IdentitySources = new[] { IdentitySource.Header("authorization") },
      ResultsCacheTtl = Duration.Seconds(0),
    });
  }

  private void AllowSsm(AppFunction function, string resource, bool allowPut)
  {
    var actions = new List<string>
    {
      "ssm:GetParametersByPath",
    };

    if (allowPut)
    {
      actions.Add("ssm:PutParameter");
    }

    var ssmPolicy = new PolicyStatement(new PolicyStatementProps
    {
      Effect = Effect.ALLOW,
      Actions = actions.ToArray(),
      Resources = new[]
      {
        $"arn:aws:ssm:{Region}:{Account}:parameter{resource}",
      },
    });

    function.AddToRolePolicy(ssmPolicy);
  }

  private void HandleAccountResource(
    Amazon.CDK.AWS.APIGateway.Resource accountResource,
    RestApi api,
    ITable applicationTable,
    RequestAuthorizer authorizer)
  {
    // Create
    var createAccountFunction = new AppFunction(this, "CreateAccount", new AppFunction.Props(
      "CreateAccount::App.Api.CreateAccount.Function::FunctionHandler",
      _tableName
    ));
    applicationTable.GrantReadWriteData(createAccountFunction);
    accountResource.AddMethod("POST", new LambdaIntegration(createAccountFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
      RequestValidator = new RequestValidator(this, "CreateAccountValidator", new RequestValidatorProps
      {
        RestApi = api,
        ValidateRequestBody = true,
      }),
      RequestModels = new Dictionary<string, IModel>
      {
        { "application/json", new CreateAccountModel(this, api) },
      },
    });

    // List
    var listAccountsFunction = new AppFunction(this, "ListAccounts", new AppFunction.Props(
      "ListAccounts::App.Api.ListAccounts.Function::FunctionHandler",
      _tableName
    ));
    applicationTable.GrantReadWriteData(listAccountsFunction);
    accountResource.AddMethod("GET", new LambdaIntegration(listAccountsFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
    });
  }

  private void HandleEventResource(
    Amazon.CDK.AWS.APIGateway.Resource eventResource,
    ITable applicationTable,
    RequestAuthorizer authorizer)
  {
    // Create
    var createEventFunction = new AppFunction(this, "CreateEvent", new AppFunction.Props(
      "CreateEvent::App.Api.CreateEvent.Function::FunctionHandler",
      _tableName
    ));
    applicationTable.GrantReadWriteData(createEventFunction);
    eventResource.AddMethod("POST", new LambdaIntegration(createEventFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
    });

    // Delete
    var deleteEventFunction = new AppFunction(this, "DeleteEvent", new AppFunction.Props(
      "DeleteEvent::App.Api.DeleteEvent.Function::FunctionHandler",
      _tableName
    ));
    applicationTable.GrantReadWriteData(deleteEventFunction);
    eventResource.AddMethod("DELETE", new LambdaIntegration(deleteEventFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
    });

    // Edit
    var editEventFunction = new AppFunction(this, "EditEvent", new AppFunction.Props(
      "EditEvent::App.Api.EditEvent.Function::FunctionHandler",
      _tableName
    ));
    applicationTable.GrantReadWriteData(editEventFunction);
    eventResource.AddMethod("PUT", new LambdaIntegration(editEventFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
    });

    // List
    var listEventsFunction = new AppFunction(this, "ListEvents", new AppFunction.Props(
      "ListEvents::App.Api.ListEvents.Function::FunctionHandler",
      _tableName
    ));
    applicationTable.GrantReadData(listEventsFunction);
    eventResource.AddMethod("GET", new LambdaIntegration(listEventsFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
    });
  }

  private void HandleTagResource(
    Amazon.CDK.AWS.APIGateway.Resource tagResource,
    ITable applicationTable,
    RequestAuthorizer authorizer)
  {
    // List
    var listTagsFunction = new AppFunction(this, "ListTags", new AppFunction.Props(
      "ListTags::App.Api.ListTags.Function::FunctionHandler",
      _tableName
    ));
    applicationTable.GrantReadData(listTagsFunction);
    tagResource.AddMethod("GET", new LambdaIntegration(listTagsFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
    });
  }
}
