using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleHub.ProfileService.Api.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BattleHub.ProfileService.Api.Services;

public class Auth0ManagementClient : IAuth0ManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly Auth0Options _options;
    private readonly ILogger<Auth0ManagementClient> _logger;

    private static readonly HashSet<(string ApiIdentifier, string PermissionCode)> AllowedPermissions = new()
    {
        ("https://api.battlehub.local/typing", "games.typing.play"),
        ("https://api.battlehub.local/trivia", "games.trivia.play"),
        ("https://api.battlehub.local/memory", "games.memory.play"),
        ("https://api.battlehub.local/profile", "matches.create"),
        ("https://api.battlehub.local/matchmaking", "matches.create")
    };

    public Auth0ManagementClient(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        IOptions<Auth0Options> options,
        ILogger<Auth0ManagementClient> logger)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GetUserEffectivePermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("El userId no puede ser nulo ni vacío.", nameof(userId));
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var encodedUserId = Uri.EscapeDataString(userId);
        var matchedPermissions = new HashSet<string>(StringComparer.Ordinal);
        var domain = _options.Domain;

        int page = 0;
        const int perPage = 100;

        while (true)
        {
            var requestUri = $"https://{domain}/api/v2/users/{encodedUserId}/permissions?per_page={perPage}&page={page}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Fallo al consultar permisos en Auth0 Management API para el usuario.");
                throw new Auth0ManagementException("Fallo al consultar permisos en Auth0 Management API.", ex);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Auth0 Management API devolvió status {StatusCode} al consultar permisos.", response.StatusCode);
                    throw new Auth0ManagementException($"Auth0 Management API devolvió error {response.StatusCode}.");
                }

                List<Auth0PermissionItem>? items;
                try
                {
                    items = await response.Content.ReadFromJsonAsync<List<Auth0PermissionItem>>(cancellationToken: cancellationToken);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Respuesta JSON mal formada al consultar permisos en Auth0 Management API.");
                    throw new Auth0ManagementException("Respuesta inválida de Auth0 Management API.", ex);
                }

                if (items == null || items.Count == 0)
                {
                    break;
                }

                foreach (var item in items)
                {
                    if (!string.IsNullOrWhiteSpace(item.ResourceServerIdentifier) &&
                        !string.IsNullOrWhiteSpace(item.PermissionName))
                    {
                        if (AllowedPermissions.Contains((item.ResourceServerIdentifier, item.PermissionName)))
                        {
                            matchedPermissions.Add(item.PermissionName);
                        }
                    }
                }

                if (items.Count < perPage)
                {
                    break;
                }
            }

            page++;
        }

        return matchedPermissions.ToList();
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var domain = _options.Domain;
        var clientId = _options.Management.ClientId;
        var clientSecret = _options.Management.ClientSecret;

        if (string.IsNullOrWhiteSpace(domain) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
        {
            _logger.LogError("Credenciales M2M de Auth0 incompletas. Verifique Domain, ClientId y ClientSecret.");
            throw new Auth0ManagementException("Credenciales M2M de Auth0 no configuradas.");
        }

        var cacheKey = $"Auth0_M2M_Token_{domain}_{clientId}";
        if (_memoryCache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return cachedToken;
        }

        var audience = string.IsNullOrWhiteSpace(_options.Management.Audience)
            ? $"https://{domain}/api/v2/"
            : _options.Management.Audience;

        var tokenRequest = new
        {
            client_id = clientId,
            client_secret = clientSecret,
            audience = audience,
            grant_type = "client_credentials"
        };

        var tokenUri = $"https://{domain}/oauth/token";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(tokenUri, tokenRequest, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Fallo al solicitar token M2M a Auth0.");
            throw new Auth0ManagementException("Fallo de red o timeout al solicitar token M2M a Auth0.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Auth0 devolvió código {StatusCode} al solicitar token M2M.", response.StatusCode);
                throw new Auth0ManagementException($"Error al solicitar token M2M a Auth0: HTTP {response.StatusCode}");
            }

            Auth0TokenResponse? tokenData;
            try
            {
                tokenData = await response.Content.ReadFromJsonAsync<Auth0TokenResponse>(cancellationToken: cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Respuesta JSON mal formada al solicitar token M2M a Auth0.");
                throw new Auth0ManagementException("Respuesta de token M2M de Auth0 inválida.", ex);
            }

            if (string.IsNullOrWhiteSpace(tokenData?.AccessToken))
            {
                _logger.LogError("La respuesta de Auth0 no incluyó un access_token.");
                throw new Auth0ManagementException("La respuesta de token M2M de Auth0 no contiene un access_token.");
            }

            var expiresIn = tokenData.ExpiresIn > 0 ? tokenData.ExpiresIn : 86400;
            var cacheDuration = TimeSpan.FromSeconds(Math.Max(30, expiresIn - 60));
            _memoryCache.Set(cacheKey, tokenData.AccessToken, cacheDuration);

            return tokenData.AccessToken;
        }
    }

    private sealed record Auth0TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string? TokenType
    );

    private sealed record Auth0PermissionItem(
        [property: JsonPropertyName("permission_name")] string? PermissionName,
        [property: JsonPropertyName("resource_server_name")] string? ResourceServerName,
        [property: JsonPropertyName("resource_server_identifier")] string? ResourceServerIdentifier
    );
}
