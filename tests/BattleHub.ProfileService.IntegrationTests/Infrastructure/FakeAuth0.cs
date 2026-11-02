using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BattleHub.ProfileService.IntegrationTests.Infrastructure;

/// <summary>
/// Simula las dos llamadas salientes que el servicio hace a Auth0: el token M2M
/// (<c>POST /oauth/token</c>) y los permisos del usuario
/// (<c>GET /api/v2/users/{sub}/permissions</c>), con la misma forma y paginación que la
/// Management API.
///
/// R-18: es lo único externo que se reemplaza. El <c>Auth0ManagementClient</c> real hace las
/// peticiones, recorre las páginas, guarda el token en caché y filtra por API y permiso; solo
/// cambia quién responde al otro lado del <see cref="HttpClient"/>.
/// </summary>
public sealed class FakeAuth0
{
    public const string ClientId = "cliente-m2m-de-prueba";
    public const string ClientSecret = "secreto-de-prueba";
    private const string AccessToken = "token-m2m-de-prueba";
    private const int MaxPerPage = 100;

    private readonly ConcurrentDictionary<string, IReadOnlyList<Auth0Permission>> _permissions = new(StringComparer.Ordinal);
    private int _tokenRequests;

    /// <summary>
    /// Si es <c>true</c>, la Management API responde 503 a las consultas de permisos.
    /// </summary>
    public bool Unavailable { get; set; }

    public int TokenRequests => _tokenRequests;

    /// <summary>
    /// Asigna en el "tenant" los permisos del usuario, cada uno con la API que lo emite.
    /// Reemplaza los anteriores, como al quitar o agregar un permiso en el panel de Auth0.
    /// </summary>
    public void SetPermissions(string sub, params (string Api, string Permission)[] permissions)
    {
        _permissions[sub] = permissions.Select(p => new Auth0Permission(p.Permission, p.Api, p.Api)).ToList();
    }

    public void SetPermissions(string sub, IEnumerable<(string Api, string Permission)> permissions)
    {
        SetPermissions(sub, permissions.ToArray());
    }

    public HttpMessageHandler CreateHandler() => new Handler(this);

    private sealed class Handler(FakeAuth0 server) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not { Host: TestHost.Auth0Domain } uri)
            {
                throw new InvalidOperationException($"Llamada inesperada fuera del tenant de prueba: {request.RequestUri}");
            }

            if (request.Method == HttpMethod.Post && uri.AbsolutePath == "/oauth/token")
            {
                return await server.IssueTokenAsync(request, cancellationToken);
            }

            const string prefix = "/api/v2/users/";
            const string suffix = "/permissions";
            if (request.Method == HttpMethod.Get
                && uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)
                && uri.AbsolutePath.EndsWith(suffix, StringComparison.Ordinal))
            {
                var encodedSub = uri.AbsolutePath[prefix.Length..^suffix.Length];
                return server.ListPermissions(request, Uri.UnescapeDataString(encodedSub));
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private async Task<HttpResponseMessage> IssueTokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _tokenRequests);

        var body = request.Content == null
            ? (JsonElement?)null
            : await request.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        var valid = body is { } json
            && Read(json, "grant_type") == "client_credentials"
            && Read(json, "client_id") == ClientId
            && Read(json, "client_secret") == ClientSecret
            && Read(json, "audience") == $"https://{TestHost.Auth0Domain}/api/v2/";

        if (!valid)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        return JsonResponse(new { access_token = AccessToken, expires_in = 86400, token_type = "Bearer" });
    }

    private HttpResponseMessage ListPermissions(HttpRequestMessage request, string sub)
    {
        if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: AccessToken })
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        if (Unavailable)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
        var perPage = Math.Clamp(int.TryParse(query["per_page"], out var pp) ? pp : 50, 1, MaxPerPage);
        var page = int.TryParse(query["page"], out var p) ? p : 0;

        var all = _permissions.GetValueOrDefault(sub) ?? Array.Empty<Auth0Permission>();
        return JsonResponse(all.Skip(page * perPage).Take(perPage).ToList());
    }

    private static string? Read(JsonElement json, string property) =>
        json.TryGetProperty(property, out var value) ? value.GetString() : null;

    private static HttpResponseMessage JsonResponse<T>(T content) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(content) };

    private sealed record Auth0Permission(
        [property: JsonPropertyName("permission_name")] string PermissionName,
        [property: JsonPropertyName("resource_server_identifier")] string ResourceServerIdentifier,
        [property: JsonPropertyName("resource_server_name")] string ResourceServerName);
}
