﻿namespace App.Api.Shared.Models;

public class EditEventInput
{
  public string? Id { get; set; }
  public string? Title { get; set; }
  public string? Description { get; set; }
  public string[]? Tags { get; set; }
}
