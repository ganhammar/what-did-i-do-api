namespace App.Api.Shared.Models;

public class CreateEventInput
{
  public string? AccountId { get; set; }
  public string? Title { get; set; }
  public string? Description { get; set; }
  public DateTime? Date { get; set; }
  public string[]? Tags { get; set; }
}
