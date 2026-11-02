using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace BattleHub.ProfileService.IntegrationTests.Infrastructure;

/// <summary>
/// Emite tokens firmados con una clave RSA de prueba, como los que Auth0 entrega al Shell.
///
/// R-18: la API valida estos tokens con su configuración real de JwtBearer (emisor, audiencia,
/// firma y vencimiento). Lo único que se reemplaza es de dónde salen las claves públicas: en vez
/// de descargar el JWKS del tenant, se le entrega la clave de prueba.
/// </summary>
public static class TestJwt
{
    private static readonly RSA TenantRsa = RSA.Create(2048);

    public static readonly RsaSecurityKey TenantKey = new(TenantRsa) { KeyId = "clave-del-tenant-de-prueba" };

    /// <summary>
    /// Sustituye el JWKS de Auth0 por la clave de prueba. Se registra con
    /// <c>ConfigureTestServices</c>, después de la configuración de <c>Program.cs</c>.
    /// </summary>
    public static void UseTestSigningKey(JwtBearerOptions options)
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = TestHost.Auth0Issuer };
        configuration.SigningKeys.Add(TenantKey);
        options.Configuration = configuration;
        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    }

    public static string CreateToken(
        string? subject,
        string issuer = TestHost.Auth0Issuer,
        string audience = TestHost.Auth0Audience,
        SecurityKey? signingKey = null,
        DateTime? expires = null,
        DateTime? notBefore = null,
        IDictionary<string, object>? extraClaims = null)
    {
        var claims = new List<Claim>();
        if (subject != null)
        {
            claims.Add(new Claim("sub", subject));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(signingKey ?? TenantKey, SecurityAlgorithms.RsaSha256),
            Claims = extraClaims
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
