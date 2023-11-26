namespace App.Api.Shared.Infrastructure;

public class FunctionError(string propertyName, string message)
{
  public string PropertyName { get; set; } = propertyName;
  public string Message { get; set; } = message;
  public string? ErrorCode { get; set; }
}
