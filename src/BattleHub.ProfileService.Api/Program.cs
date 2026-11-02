using BattleHub.ProfileService.Api.Configuration;
using BattleHub.ProfileService.Api.Data;
using BattleHub.ProfileService.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// R-09: la configuración no tiene valores por defecto en el código, y faltar una obligatoria
// impide arrancar. Hay dos usos que construyen la aplicación sin servir peticiones y solo
// necesitan la base de datos: las herramientas de EF (`dotnet ef migrations`), que leen el
// modelo, y el servicio `migrate` del compose, que aplica las migraciones y termina. Exigirles
// el dominio del tenant o los orígenes del Shell los haría fallar por configuración que no usan.
var soloMigrar = args.Contains("--migrate");
var validarConfiguracion = !EF.IsDesignTime && !soloMigrar;

// Orígenes del navegador que pueden llamar a la API: el Shell local y el publicado.
// Las entradas vacías se ignoran, para poder dejar sin valor un origen opcional en el .env.
var corsOrigins = (builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray();
if (validarConfiguracion && corsOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Falta la configuración 'Cors:Origins'. Defina al menos Cors__Origins__0 con el origen del Shell.");
}
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

// Configuración de Auth0
builder.Services.Configure<Auth0Options>(builder.Configuration.GetSection(Auth0Options.SectionName));

// Configuración de persistencia con EF Core.
// R-12: el único motor de la API es MySQL. La base en memoria solo se permite en pruebas, que
// registran su propio proveedor al construir la aplicación de prueba.
var dbProvider = EF.IsDesignTime ? "MySQL" : builder.Configuration["DatabaseProvider"];

if (!string.Equals(dbProvider, "MySQL", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"DatabaseProvider debe ser 'MySQL' (valor actual: '{dbProvider}').");
}

// La cadena de conexión no se versiona: llega por la variable de entorno
// ConnectionStrings__DefaultConnection. Generar migraciones no necesita conectarse a MySQL.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? (EF.IsDesignTime
        ? "Server=localhost;Database=battlehub_profiles;"
        : throw new InvalidOperationException(
            "Falta la cadena de conexión 'ConnectionStrings:DefaultConnection'."));

builder.Services.AddDbContext<ProfileDbContext>(options =>
    options.UseMySQL(connectionString));

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IAuth0ManagementClient, Auth0ManagementClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IProfileService, ProfileService>();

// Configuración de OpenAPI con soporte para tokens JWT Bearer de Auth0
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes.Add("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Introduce el token JWT de Auth0 en formato: Bearer {token}"
        });

        document.Security ??= new List<OpenApiSecurityRequirement>();
        var requirement = new OpenApiSecurityRequirement();
        requirement.Add(new OpenApiSecuritySchemeReference("Bearer", document), new List<string>());
        document.Security.Add(requirement);

        return Task.CompletedTask;
    });
});

// Errores con formato ProblemDetails (RFC 9457), propuesto en el Issue 8.
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks();

// Configuración de Autenticación JWT Bearer con Auth0.
// R-09: no hay dominio ni audiencia por defecto en el código. Arrancar con un marcador haría
// que la API rechazara todos los tokens del tenant real sin decir por qué.
var auth0Domain = builder.Configuration["Auth0:Domain"];
var auth0Audience = builder.Configuration["Auth0:Audience"];

if (validarConfiguracion)
{
    if (string.IsNullOrWhiteSpace(auth0Domain))
    {
        throw new InvalidOperationException(
            "Falta la configuración 'Auth0:Domain'. Defina Auth0__Domain con el dominio del tenant.");
    }

    if (string.IsNullOrWhiteSpace(auth0Audience))
    {
        throw new InvalidOperationException(
            "Falta la configuración 'Auth0:Audience'. Defina Auth0__Audience con el Identifier de la API.");
    }

    // Los permisos de juegos viven en la API de cada juego, no en la de Profile. Si falta alguno
    // de esos identifiers, el filtro no reconocería ese permiso y el usuario vería un juego de
    // menos sin ningún error: es preferible no arrancar.
    foreach (var clave in new[]
             {
                 "Auth0:PermissionApis:Typing",
                 "Auth0:PermissionApis:Trivia",
                 "Auth0:PermissionApis:Memory"
             })
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[clave]))
        {
            throw new InvalidOperationException(
                $"Falta la configuración '{clave}'. Defina el Identifier de la API del juego en Auth0.");
        }
    }
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = $"https://{auth0Domain}/";
    options.Audience = auth0Audience;
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = $"https://{auth0Domain}/",
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// `--migrate` aplica las migraciones pendientes (incluido el catálogo de juegos) y termina sin
// levantar la API. Lo usa el servicio `migrate` de docker-compose.yml.
if (soloMigrar)
{
    using var migrationScope = app.Services.CreateScope();
    await migrationScope.ServiceProvider.GetRequiredService<ProfileDbContext>().Database.MigrateAsync();
    return;
}

// Las credenciales del M2M son deliberadamente opcionales para arrancar: el smoke test del CI
// levanta el compose con el .env de ejemplo, sin secretos, y debe poder comprobar /health y que
// un endpoint protegido responde 401. Sin ellas el servicio funciona salvo en lo que necesita
// Auth0: /me/access, /me/permissions y /me/games responden 503. Se avisa al arrancar para que
// ese 503 no haya que diagnosticarlo desde los logs de una peticion.
if (string.IsNullOrWhiteSpace(builder.Configuration["Auth0:Management:ClientId"])
    || string.IsNullOrWhiteSpace(builder.Configuration["Auth0:Management:ClientSecret"]))
{
    app.Logger.LogWarning(
        "Faltan las credenciales de la aplicación M2M (Auth0__Management__ClientId y " +
        "Auth0__Management__ClientSecret). El servicio arranca, pero no podrá consultar los " +
        "permisos en Auth0: /me/access, /me/permissions y /me/games responderán 503.");
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Redirección a HTTPS, activa por defecto. Se desactiva con Https:Redirect=false cuando la API
// solo escucha HTTP (el contenedor, o detrás de un proxy que ya maneja HTTPS): el navegador no
// acepta redirecciones en la petición previa de CORS y la llamada del Shell fallaría.
if (builder.Configuration.GetValue("Https:Redirect", true))
{
    app.UseHttpsRedirection();
}

app.UseCors();

// Orden estricto de middlewares: Autenticación antes de Autorización
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program { }
