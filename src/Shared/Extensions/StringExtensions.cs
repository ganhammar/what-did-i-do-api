using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace App.Api.Shared.Extensions;

public static partial class StringExtensions
{
  public static string UrlFriendly(this string value)
  {
    var urlFriendlyValue = value.ToLower();
    // replace diactrics
    urlFriendlyValue = ReplaceDiacritics(urlFriendlyValue);
    // remove entities
    urlFriendlyValue = RemoveEntities().Replace(urlFriendlyValue, "");
    // remove anything that is not letters, numbers, dash, or space
    urlFriendlyValue = RemoveUnwantedCharacters().Replace(urlFriendlyValue, "");
    // replace new line and tabs
    urlFriendlyValue = ReplaceNewLineAndTabs().Replace(urlFriendlyValue, "");
    // replace spaces
    urlFriendlyValue = urlFriendlyValue.Replace(' ', '-');
    // collapse dashes
    urlFriendlyValue = CollapseDashes().Replace(urlFriendlyValue, "-");
    // trim excessive dashes at the beginning
    urlFriendlyValue = urlFriendlyValue.TrimStart(['-']);
    // if it's too long, clip it
    if (urlFriendlyValue.Length > 50)
    {
      urlFriendlyValue = urlFriendlyValue[..49];
    }
    // remove trailing dashes
    urlFriendlyValue = urlFriendlyValue.TrimEnd(['-']);

    // don't allow guids
    if (Guid.TryParse(urlFriendlyValue, out _))
    {
      var random = new Random();
      urlFriendlyValue = $"{urlFriendlyValue}{random.Next(9)}";
    }

    return urlFriendlyValue;
  }

  private static string ReplaceDiacritics(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return value;
    }

    value = value.Normalize(NormalizationForm.FormD);
    var chars = value.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
    return new string(chars).Normalize(NormalizationForm.FormC);
  }

  public static string To64(this string toEncode)
  {
    byte[] toEncodeAsBytes = Encoding.ASCII.GetBytes(toEncode);
    return Convert.ToBase64String(toEncodeAsBytes);
  }

  public static string? From64(this string encodedData)
  {
    try
    {
      byte[] encodedDataAsBytes = Convert.FromBase64String(encodedData);
      return Encoding.ASCII.GetString(encodedDataAsBytes);
    }
    catch
    {
      return default;
    }
  }

  [GeneratedRegex(@"-{2,}")]
  private static partial Regex CollapseDashes();

  [GeneratedRegex(@"\t|\n|\r")]
  private static partial Regex ReplaceNewLineAndTabs();

  [GeneratedRegex(@"[^a-z0-9\-\s]")]
  private static partial Regex RemoveUnwantedCharacters();

  [GeneratedRegex(@"&\w+;")]
  private static partial Regex RemoveEntities();
}
