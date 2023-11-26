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
  private const string TableName = "what-did-i-do";
  private readonly IConfiguration _configuration;

  internal AppStack(Construct scope, string id, IStackProps props, IConfiguration configuration)
    : base(scope, id, props)
  {
    _configuration = configuration;

    // DynamoDB
    var applicationTable = Table.FromTableAttributes(this, "ApplicationTable", new TableAttributes
    {
      TableArn = $"arn:aws:dynamodb:{Region}:{Account}:table/{TableName}",
      GrantIndexPermissions = true,
    });

    // API Gateway
    var api = new RestApi(this, "what-did-i-do-api", new RestApiProps
    {
      RestApiName = "what-did-i-do-api",
      DefaultCorsPreflightOptions = new CorsOptions
      {
        AllowOrigins =
        [
          "http://localhost:3000",
        ],
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
    HandleEventResource(eventResource, api, applicationTable, authorizer);

    // Resource: Tag
    var tagResource = apiResource.AddResource("tag");
    HandleTagResource(tagResource, api, applicationTable, authorizer);

    // Output
    _ = new CfnOutput(this, "APIGWEndpoint", new CfnOutputProps
    {
      Value = api.Url,
    });
  }

  private RequestAuthorizer CreateAuthorizerFunction()
  {
    _ = new StringParameter(this, "AuthorizerSigningCertificateParameter", new StringParameterProps
    {
      ParameterName = "/WDID/Authorizer/AuthorizationOptions/SigningCertificate",
      StringValue = _configuration.GetSection("Authorizer").GetValue<string>("SigningCertificate")!,
      Tier = ParameterTier.STANDARD,
    });
    _ = new StringParameter(this, "AuthorizerEncryptionCertificateParameter", new StringParameterProps
    {
      ParameterName = "/WDID/Authorizer/AuthorizationOptions/EncryptionCertificate",
      StringValue = _configuration.GetSection("Authorizer").GetValue<string>("EncryptionCertificate")!,
      Tier = ParameterTier.STANDARD,
    });

    var authorizerFunction = new AppFunction(this, "App.Authorizer", new AppFunction.Props(
      "App.Authorizer::App.Authorizer.Function::FunctionHandler"
    ));

    AllowSsm(authorizerFunction, "/WDID/Authorizer*", false);

    return new RequestAuthorizer(this, "ApiAuthorizer", new RequestAuthorizerProps
    {
      Handler = authorizerFunction,
      IdentitySources = [IdentitySource.Header("authorization")],
      ResultsCacheTtl = Duration.Minutes(5),
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
      Actions = [.. actions],
      Resources =
      [
        $"arn:aws:ssm:{Region}:{Account}:parameter{resource}",
      ],
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
      TableName
    ));
    applicationTable.GrantReadWriteData(createAccountFunction);
    accountResource.AddMethod("POST", new LambdaIntegration(createAccountFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
      RequestValidator = new RequestValidator(this, "CreateAccountValidator", new RequestValidatorProps
      {
        ValidateRequestBody = true,
        RestApi = api,
      }),
      RequestModels = new Dictionary<string, IModel>
      {
        { "application/json", new CreateAccountModel(this, api) },
      },
    });

    // List
    var listAccountsFunction = new AppFunction(this, "ListAccounts", new AppFunction.Props(
      "ListAccounts::App.Api.ListAccounts.Function::FunctionHandler",
      TableName
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
    RestApi api,
    ITable applicationTable,
    RequestAuthorizer authorizer)
  {
    // Create
    var createEventFunction = new AppFunction(this, "CreateEvent", new AppFunction.Props(
      "CreateEvent::App.Api.CreateEvent.Function::FunctionHandler",
      TableName
    ));
    applicationTable.GrantReadWriteData(createEventFunction);
    eventResource.AddMethod("POST", new LambdaIntegration(createEventFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
      RequestValidator = new RequestValidator(this, "CreateEventValidator", new RequestValidatorProps
      {
        ValidateRequestBody = true,
        RestApi = api,
      }),
      RequestModels = new Dictionary<string, IModel>
      {
        { "application/json", new CreateEventModel(this, api) },
      },
    });

    // Delete
    var deleteEventFunction = new AppFunction(this, "DeleteEvent", new AppFunction.Props(
      "DeleteEvent::App.Api.DeleteEvent.Function::FunctionHandler",
      TableName
    ));
    applicationTable.GrantReadWriteData(deleteEventFunction);
    eventResource.AddMethod("DELETE", new LambdaIntegration(deleteEventFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
      RequestValidator = new RequestValidator(this, "DeleteEventValidator", new RequestValidatorProps
      {
        ValidateRequestParameters = true,
        RestApi = api,
      }),
      RequestParameters = new Dictionary<string, bool>
      {
        { "method.request.querystring.id", true },
      },
    });

    // Edit
    var editEventFunction = new AppFunction(this, "EditEvent", new AppFunction.Props(
      "EditEvent::App.Api.EditEvent.Function::FunctionHandler",
      TableName
    ));
    applicationTable.GrantReadWriteData(editEventFunction);
    eventResource.AddMethod("PUT", new LambdaIntegration(editEventFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
      RequestValidator = new RequestValidator(this, "EditEventValidator", new RequestValidatorProps
      {
        ValidateRequestBody = true,
        RestApi = api,
      }),
      RequestModels = new Dictionary<string, IModel>
      {
        { "application/json", new EditEventModel(this, api) },
      },
    });

    // List
    var listEventsFunction = new AppFunction(this, "ListEvents", new AppFunction.Props(
      "ListEvents::App.Api.ListEvents.Function::FunctionHandler",
      TableName
    ));
    applicationTable.GrantReadData(listEventsFunction);
    eventResource.AddMethod("GET", new LambdaIntegration(listEventsFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
      RequestValidator = new RequestValidator(this, "ListEventValidator", new RequestValidatorProps
      {
        ValidateRequestParameters = true,
        RestApi = api,
      }),
      RequestParameters = new Dictionary<string, bool>
      {
        { "method.request.querystring.accountId", true },
        { "method.request.querystring.limit", true },
      },
    });
  }

  private void HandleTagResource(
    Amazon.CDK.AWS.APIGateway.Resource tagResource,
    RestApi api,
    ITable applicationTable,
    RequestAuthorizer authorizer)
  {
    // List
    var listTagsFunction = new AppFunction(this, "ListTags", new AppFunction.Props(
      "ListTags::App.Api.ListTags.Function::FunctionHandler",
      TableName
    ));
    applicationTable.GrantReadData(listTagsFunction);
    tagResource.AddMethod("GET", new LambdaIntegration(listTagsFunction), new MethodOptions
    {
      AuthorizationType = AuthorizationType.CUSTOM,
      Authorizer = authorizer,
      RequestValidator = new RequestValidator(this, "ListTagsValidator", new RequestValidatorProps
      {
        ValidateRequestParameters = true,
        RestApi = api,
      }),
      RequestParameters = new Dictionary<string, bool>
      {
        { "method.request.querystring.accountId", true },
      },
    });
  }
}
