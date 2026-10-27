using BattleHub.ProfileService.Api.Configuration;
using BattleHub.ProfileService.Api.Data;
using BattleHub.ProfileService.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Orígenes del navegador que pueden llamar a la API (ADR-006): el Shell local y el publicado.
// Las entradas vacías se ignoran, para poder dejar sin valor un origen opcional en el .env.
var corsOrigins = (builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4000"])
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

// Configuración de Auth0
builder.Services.Configure<Auth0Options>(builder.Configuration.GetSection(Auth0Options.SectionName));

// Configuración de persistencia con EF Core.
// Fuera de Development, DatabaseProvider es obligatorio: un despliegue mal configurado
// falla al arrancar en vez de guardar los perfiles en memoria y perderlos al reiniciar.
var dbProvider = builder.Configuration["DatabaseProvider"];
if (EF.IsDesignTime)
{
    dbProvider = "MySQL";
}
else if (string.IsNullOrWhiteSpace(dbProvider) && builder.Environment.IsDevelopment())
{
    dbProvider = "InMemory";
}

bool useMySql = string.Equals(dbProvider, "MySQL", StringComparison.OrdinalIgnoreCase);
bool useInMemory = string.Equals(dbProvider, "InMemory", StringComparison.OrdinalIgnoreCase);

if (!useMySql && !useInMemory)
{
    throw new InvalidOperationException(
        $"DatabaseProvider debe ser 'MySQL' o 'InMemory' (valor actual: '{dbProvider}').");
}

if (useMySql)
{
    // La cadena de conexión no se versiona: va en user-secrets o en la variable de entorno
    // ConnectionStrings__DefaultConnection. Generar migraciones no necesita conectarse a MySQL.
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? (EF.IsDesignTime
            ? "Server=localhost;Database=battlehub_profiles;"
            : throw new InvalidOperationException(
                "Falta la cadena de conexión 'ConnectionStrings:DefaultConnection'."));

    builder.Services.AddDbContext<ProfileDbContext>(options =>
        options.UseMySQL(connectionString));
}
else
{
    var inMemoryDbName = builder.Configuration["InMemoryDatabaseName"] ?? "BattleHubProfiles";
    builder.Services.AddDbContext<ProfileDbContext>(options =>
        options.UseInMemoryDatabase(inMemoryDbName));
}

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

// Configuración de Autenticación JWT Bearer con Auth0
var auth0Domain = builder.Configuration["Auth0:Domain"] ?? "auth.battlehub.local";
var auth0Audience = builder.Configuration["Auth0:Audience"] ?? "https://api.battlehub.local/profile";

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

// `--migrate` aplica las migraciones pendientes (incluidos los datos iniciales de permisos y
// juegos) y termina sin levantar la API. Lo usa el servicio `migrate` de docker-compose.yml.
if (args.Contains("--migrate"))
{
    if (!useMySql)
    {
        throw new InvalidOperationException("--migrate solo aplica con DatabaseProvider=MySQL.");
    }

    using var migrationScope = app.Services.CreateScope();
    await migrationScope.ServiceProvider.GetRequiredService<ProfileDbContext>().Database.MigrateAsync();
    return;
}

if (!useMySql)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ProfileDbContext>().Database.EnsureCreatedAsync();
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
