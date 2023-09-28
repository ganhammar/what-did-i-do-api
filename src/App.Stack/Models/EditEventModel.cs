using Amazon.CDK.AWS.APIGateway;
using Constructs;

namespace AppStack.Models;

public class EditEventModel : Model
{
  public EditEventModel(Construct scope, RestApi api)
    : base(scope, "EditEventModel", new ModelProps
    {
      RestApi = api,
      ContentType = "application/json",
      Schema = new JsonSchema
      {
        Type = JsonSchemaType.OBJECT,
        Required = new[] { "id", "title" },
      },
    })
  { }
}
