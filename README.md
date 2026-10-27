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
/src/BattleHub.ProfileService.Api            → API REST (ASP.NET Core)
/tests/BattleHub.ProfileService.UnitTests    → pruebas unitarias (Category=Unit)
/tests/BattleHub.ProfileService.IntegrationTests → pruebas de integración (Category=Integration)
/.github/workflows                           → pipeline de CI/CD
Dockerfile, docker-compose.yml, .env.example → ejecución con Docker
```

## Cómo correrlo localmente

Requisitos previos:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MySQL 8 (local o en contenedor con Docker), para cualquier entorno distinto de Development
- Acceso al tenant de Auth0 del proyecto

Compilar y ejecutar la API:

```bash
dotnet restore
dotnet build
dotnet run --project src/BattleHub.ProfileService.Api
```

### Base de datos

El proveedor se elige con la variable `DatabaseProvider`:

| Valor | Uso |
|---|---|
| `MySQL` | Motor oficial del servicio. Requiere `ConnectionStrings:DefaultConnection`. |
| `InMemory` | Solo para desarrollo rápido y pruebas. Los perfiles se pierden al reiniciar. |

- En **Development**, si `DatabaseProvider` no está definido se usa `InMemory`.
- En **cualquier otro entorno**, `DatabaseProvider` es obligatorio. Si falta, tiene un valor desconocido o falta la cadena de conexión de MySQL, la API no arranca y muestra el motivo.

La cadena de conexión no se versiona. Para usar MySQL en local, se configura con user-secrets:

```bash
dotnet user-secrets --project src/BattleHub.ProfileService.Api set "DatabaseProvider" "MySQL"
dotnet user-secrets --project src/BattleHub.ProfileService.Api set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=battlehub_profiles;User=<usuario>;Password=<contraseña>;"
```

En despliegue se usan las variables de entorno `DatabaseProvider=MySQL` y `ConnectionStrings__DefaultConnection`.

Las migraciones (que incluyen los permisos y el catálogo de juegos) se aplican de forma explícita, nunca al arrancar la API:

```bash
dotnet run --project src/BattleHub.ProfileService.Api -- --migrate
```

## Ejecutar con Docker

Requisito: [Docker Desktop](https://www.docker.com/products/docker-desktop/) o Docker Engine con Docker Compose.

1. Copiar el archivo de variables y completar los valores (contraseñas de MySQL, dominio y audience de Auth0):

   ```bash
   cp .env.example .env
   ```

2. Levantar todo:

   ```bash
   docker compose up --build
   ```

   Se inician tres servicios, en este orden:

   | Servicio | Qué hace |
   |---|---|
   | `mysql` | MySQL 8.4 con los datos en el volumen `profile-mysql-data`. |
   | `migrate` | Aplica las migraciones y carga los datos iniciales; termina al acabar. |
   | `api` | La API, en `http://localhost:5220`. Arranca solo si `migrate` terminó bien. |

3. Comprobar que funciona:

   ```bash
   curl http://localhost:5220/health
   curl -i http://localhost:5220/api/profiles/me
   ```

   El primero responde `Healthy` y el segundo `401` (no se envió token).

4. Detener y borrar todo, incluidos los datos:

   ```bash
   docker compose down -v
   ```

La imagen no contiene secretos, corre con un usuario sin privilegios y usa versiones fijas de las imágenes base. El CI construye la imagen y hace esta misma prueba en cada Pull Request.

La API queda en `http://localhost:5220` (puerto provisional hasta que se acuerden los puertos del proyecto). Endpoints disponibles:

- `GET /health` → estado del servicio
- `GET /openapi/v1.json` → documento OpenAPI (solo en entorno Development)

Ejecutar las pruebas:

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

Las pruebas de integración contra MySQL real levantan un contenedor con [Testcontainers](https://dotnet.testcontainers.org/), así que necesitan Docker. Sin Docker se saltan con un aviso; en el CI se ejecutan siempre.

### Cobertura

El CI exige al menos **85% de cobertura de líneas** del código de la API (sin contar migraciones ni código generado) y falla si baja de ese valor. El resultado aparece en el resumen de cada ejecución del CI.

Para medirla en local (ReportGenerator está fijado en `dotnet-tools.json`):

```bash
dotnet tool restore
dotnet test --collect:"XPlat Code Coverage" --results-directory coverage/raw
dotnet reportgenerator "-reports:coverage/raw/**/coverage.cobertura.xml" "-targetdir:coverage/report" "-reporttypes:TextSummary" "-assemblyfilters:+BattleHub.ProfileService.Api" "-filefilters:-*/Migrations/*;-*/obj/*"
cat coverage/report/Summary.txt
```

En local la cifra puede ser algo menor que en el CI, porque sin Docker se saltan las pruebas contra MySQL.

Los secretos (cadena de conexión, dominio y audience de Auth0) no se versionan. Se configuran con `dotnet user-secrets` o variables de entorno.

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
