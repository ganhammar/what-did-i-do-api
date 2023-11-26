using Amazon.CDK.AWS.APIGateway;
using Constructs;

namespace AppStack.Models;

public class EditEventModel(Construct scope, RestApi api)
  : Model(scope, "EditEventModel", new ModelProps
  {
    RestApi = api,
    ContentType = "application/json",
    Schema = new JsonSchema
    {
      Type = JsonSchemaType.OBJECT,
      Required = ["id", "title"],
    },
  })
{
}
