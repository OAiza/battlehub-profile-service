# battlehub-profile-service

Servicio **Identity & Profile** del proyecto BattleHub, a cargo del **Equipo 1**.

Sincroniza a los usuarios autenticados con Auth0, crea su perfil local y expone sus permisos y los juegos que tienen habilitados. El equipo también administra el tenant de Auth0 que comparte todo el proyecto.

Los contratos que implementa este servicio están definidos en [battlehub-contracts](https://github.com/javiercoulon-public/battlehub-contracts). Cualquier duda sobre el contrato se resuelve con un Issue en ese repositorio.

## Stack

- Backend: .NET 10 (ASP.NET Core Web API)
- Base de datos: MySQL
- Identidad: Auth0 (JWT)
- CI: GitHub Actions

## API

Base URL: `/api/profiles`

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/profiles/sync` | Sincroniza el usuario autenticado (crea el perfil local si no existe) |
| GET | `/api/profiles/me` | Obtiene el perfil actual |
| GET | `/api/profiles/me/permissions` | Obtiene los claims/permisos del usuario |
| GET | `/api/profiles/me/games` | Obtiene el catálogo de juegos habilitados para el usuario |

Todas las fechas se manejan en UTC, formato ISO-8601 con sufijo `Z` (ejemplo: `2026-09-02T20:00:00Z`).

## Estructura del repositorio

```text
BattleHub.ProfileService.slnx                → solución
global.json                                  → versión del SDK de .NET (10)
dotnet-tools.json                            → herramientas locales (dotnet-ef)
/src/BattleHub.ProfileService.Api            → API REST (ASP.NET Core)
    /Domain/Entities                         → entidades del modelo de datos
    /Data                                    → DbContext, configuraciones, seed y migraciones
/tests/BattleHub.ProfileService.UnitTests    → pruebas unitarias (Category=Unit)
/tests/BattleHub.ProfileService.IntegrationTests → pruebas de integración (Category=Integration)
/.github/workflows                           → pipeline de CI/CD
```

## Cómo correrlo localmente

Requisitos previos:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MySQL 8.0 o superior (local o en contenedor con Docker)
- Acceso al tenant de Auth0 del proyecto

Compilar y ejecutar la API:

```bash
dotnet restore
dotnet build
dotnet run --project src/BattleHub.ProfileService.Api
```

La API queda en `http://localhost:5220` (puerto provisional hasta que se acuerden los puertos del proyecto). Endpoints disponibles:

- `GET /health` → estado del servicio
- `GET /openapi/v1.json` → documento OpenAPI (solo en entorno Development)

Ejecutar las pruebas:

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

Los secretos (cadena de conexión, dominio y audience de Auth0) no se versionan. Se configuran con `dotnet user-secrets` o variables de entorno.

## Persistencia (MySQL)

ORM: **EF Core 10** con el provider oficial de Oracle (`MySql.EntityFrameworkCore`). Pomelo todavía no
publica una versión estable para EF Core 10, por eso no se usa aquí.

### Modelo de datos

| Tabla | Descripción |
|---|---|
| `user_profiles` | Perfil local del usuario. `auth0_user_id` (el claim `sub`) es la clave natural con la que `/sync` resuelve el perfil; tiene índice único, igual que `email`. |
| `permissions` | Catálogo de permisos de negocio (`matches.create`, `games.trivia.play`, …). `granted_by_default` marca los que recibe todo perfil nuevo. |
| `games` | Catálogo de juegos. `code` coincide con el `gameType` del contrato (`typing`, `trivia`, `memory`) y `required_permission_code` es el permiso que habilita cada juego. |
| `user_profile_permissions` | Permisos concedidos a cada perfil (tabla de unión). Se borra en cascada junto con el perfil. |

`GET /api/profiles/me/games` se resuelve cruzando `games` con los permisos del usuario: un juego aparece
si está activo y el usuario tiene su `required_permission_code`.

Todas las fechas se guardan en UTC en columnas `datetime(6)`. Un conversor de valor fuerza
`DateTimeKind.Utc` al leer, para que la serialización salga en ISO-8601 con sufijo `Z`.

### Datos iniciales

El catálogo de los tres juegos y los cuatro permisos por defecto viajan **dentro de la migración**
(`HasData`), así que se aplican solos con `dotnet ef database update`, sin scripts manuales.
Están definidos en `src/BattleHub.ProfileService.Api/Data/SeedData.cs`.

### Cadena de conexión

No se versiona. En local se configura con user-secrets:

```bash
dotnet user-secrets --project src/BattleHub.ProfileService.Api   set "ConnectionStrings:ProfileDb" "server=localhost;port=3306;database=battlehub_profile;user=battlehub;password=TU_PASSWORD"
```

En CI o despliegue se usa la variable de entorno `ConnectionStrings__ProfileDb`.

### Migraciones

`dotnet-ef` está fijado en `dotnet-tools.json`, así que todo el equipo usa la misma versión:

```bash
dotnet tool restore
dotnet ef database update --project src/BattleHub.ProfileService.Api
```

Crear una migración nueva:

```bash
dotnet ef migrations add <NombreDeLaMigracion>   --project src/BattleHub.ProfileService.Api --output-dir Data/Migrations
```

Generar el script SQL (útil para revisar el cambio en el PR o aplicarlo a mano):

```bash
dotnet ef migrations script --idempotent --project src/BattleHub.ProfileService.Api
```

## Convenciones

- **Rama `main` protegida**: no se permite push directo; todo cambio entra por Pull Request con el CI en verde y al menos 1 aprobación.
- **Commits semánticos**: `<tipo>(<alcance opcional>): <descripción>`. Tipos permitidos: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `ci`, `perf`.
  - Ejemplo: `feat(profile): agregar endpoint POST /api/profiles/sync`
- **Pull Requests**: título en formato de commit semántico, referencia a la sección del contrato que implementa y evidencia de pruebas.
- **Pruebas**: unitarias (`Category=Unit`) e integración (`Category=Integration`) contra una base de datos real o en contenedor, ejecutadas en el CI.

## Equipo 1

| Integrante | Rol |
|---|---|
| _Por completar_ | Auth0 y seguridad |
| _Por completar_ | API y lógica de negocio |
| _Por completar_ | Persistencia (MySQL) |
| _Por completar_ | Calidad y CI |
