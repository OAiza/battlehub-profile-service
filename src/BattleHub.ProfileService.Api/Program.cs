using BattleHub.ProfileService.Api.Configuration;
using BattleHub.ProfileService.Api.Data;
using BattleHub.ProfileService.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Configuración de Auth0
builder.Services.Configure<Auth0Options>(builder.Configuration.GetSection(Auth0Options.SectionName));

// Configuración de persistencia con EF Core
var dbProvider = builder.Configuration["DatabaseProvider"];
bool useMySql = string.Equals(dbProvider, "MySQL", StringComparison.OrdinalIgnoreCase) || EF.IsDesignTime;

if (useMySql)
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=localhost;Database=battlehub_profiles;User=root;Password=root;";

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
var auth0Audience = builder.Configuration["Auth0:Audience"] ?? "https://api.battlehub.com";

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

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Orden estricto de middlewares: Autenticación antes de Autorización
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program { }
