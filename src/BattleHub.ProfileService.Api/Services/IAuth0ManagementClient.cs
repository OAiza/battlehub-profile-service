namespace BattleHub.ProfileService.Api.Services;

public class Auth0ManagementException : Exception
{
    public Auth0ManagementException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public interface IAuth0ManagementClient
{
    Task<IReadOnlyList<string>> GetUserEffectivePermissionsAsync(string userId, CancellationToken cancellationToken = default);
}
