namespace App.Api.Shared.Models;

public class ListEventsInput
{
  public string? AccountId { get; set; }
  public DateTime? FromDate { get; set; }
  public DateTime? ToDate { get; set; }
  public int Limit { get; set; }
  public string? Tag { get; set; }
  public string? PaginationToken { get; set; }
}
