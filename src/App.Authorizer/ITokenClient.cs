namespace App.Authorizer;

public interface ITokenClient
{
  public Task<Result> Validate(AuthorizationOptions authorizationOptions, string token);
}
