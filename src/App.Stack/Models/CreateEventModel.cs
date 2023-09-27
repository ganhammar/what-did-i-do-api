using Amazon.CDK.AWS.APIGateway;
using Constructs;

namespace AppStack.Models;

public class CreateEventModel : Model
{
  public CreateEventModel(Construct scope, RestApi api)
    : base(scope, "CreateEventModel", new ModelProps
    {
      RestApi = api,
      ContentType = "application/json",
      Schema = new JsonSchema
      {
        Type = JsonSchemaType.OBJECT,
        Required = new[] { "accountId", "title" },
      },
    })
  { }
}
