namespace App.Api.Shared.Infrastructure;

public class FunctionError
{
  public FunctionError(string propertyName, string message)
  {
    PropertyName = propertyName;
    Message = message;
  }

  public string PropertyName { get; set; }
  public string Message { get; set; }
  public string? ErrorCode { get; set; }
}
