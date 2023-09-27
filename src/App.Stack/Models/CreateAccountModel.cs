using Amazon.CDK.AWS.APIGateway;
using Constructs;

namespace AppStack.Models;

public class CreateAccountModel : Model
{
  public CreateAccountModel(Construct scope, RestApi api)
    : base(scope, "CreateAccountModel", new ModelProps
    {
      RestApi = api,
      ContentType = "application/json",
      Schema = new JsonSchema
      {
        Type = JsonSchemaType.OBJECT,
        Required = new[] { "name" },
      },
    })
  { }
}
