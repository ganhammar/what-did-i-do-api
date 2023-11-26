namespace App.Api.Shared.Models;

public class ListEventsResult
{
  public string? PaginationToken { get; set; }
  public List<EventDto> Items { get; set; } = new();
}
