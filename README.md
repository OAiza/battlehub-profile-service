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
/src                → código fuente
/tests              → pruebas unitarias e integración
/.github/workflows  → pipeline de CI/CD
```

## Cómo correrlo localmente

> Pendiente: se completará cuando exista la solución de .NET.

Requisitos previos:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MySQL (local o en contenedor con Docker)
- Acceso al tenant de Auth0 del proyecto

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
