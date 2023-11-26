using Amazon.CDK.AWS.APIGateway;
using Constructs;

namespace AppStack.Models;

public class CreateAccountModel(Construct scope, RestApi api)
  : Model(scope, "CreateAccountModel", new ModelProps
  {
    RestApi = api,
    ContentType = "application/json",
    Schema = new JsonSchema
    {
      Type = JsonSchemaType.OBJECT,
      Required = ["name"],
    },
  })
{
}
