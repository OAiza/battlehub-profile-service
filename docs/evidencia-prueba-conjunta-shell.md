# Evidencia de la prueba conjunta con el Shell (R-43, R-49)

Lista de evidencia que pidió el Equipo 3 para el flujo del Shell con el Profile Service. Cada punto tiene dos partes:

- **Prueba automática:** la prueba del repositorio que lo comprueba en cada PR, con la API real, MySQL real, un JWT firmado y la Management API de Auth0 simulada.
- **Prueba con el Shell:** lo que se comprueba a mano con el tenant real, los 3 usuarios de demostración y el Shell corriendo.

Las pruebas automáticas están en `tests/BattleHub.ProfileService.IntegrationTests/Controllers/ProfilesControllerTests.cs` salvo que se indique otro archivo.

## Cómo levantar el sistema para la prueba

```bash
cp .env.example .env      # completar AUTH0_MGMT_CLIENT_ID y AUTH0_MGMT_CLIENT_SECRET con la aplicación M2M
docker compose up --build
```

La API queda en `http://localhost:5220`. Para las consultas a mano se usa el access token que el Shell obtiene al iniciar sesión (en el navegador: herramientas de desarrollo, pestaña Red, cabecera `Authorization` de la llamada a `/api/profiles/me/access`).

```bash
TOKEN="<access token del usuario>"
curl -i -H "Authorization: Bearer $TOKEN" http://localhost:5220/api/profiles/me/access
```

**No pegar tokens ni secretos en el PR, en issues ni en capturas.**

## Lista de evidencia

| # | Evidencia | Prueba automática | Prueba con el Shell | Resultado |
|---|---|---|---|---|
| 1 | Consulta real de permisos con la aplicación M2M y `read:users` | `GetAccess_ReutilizaElTokenM2MEntreConsultas`, `GetAccess_RecorreTodasLasPaginasDeAuth0`; la petición real se prueba en `UnitTests/Services/Auth0ManagementClientTests.cs` | Con `AUTH0_MGMT_*` reales, `curl` a `/me/access` con el token del usuario 3 devuelve sus permisos | Pendiente |
| 2 | `/me/access` respeta la identidad del token y el formato acordado | `GetAccess_FormatoJson_CoincideConElContratoDelShell`, `GetAccess_RespetaLaIdentidadDelToken` | `userId` de la respuesta = `sub` del usuario que inició sesión | Pendiente |
| 3 | `/sync` no concede juegos ni restaura permisos retirados | `Sync_NoConcedeJuegosNiPermisos`, `PermisoRetiradoEnAuth0_SeReflejaYNoReapareceAlVolverASincronizar` | Usuario 1 inicia sesión (el Shell llama a `/sync`) y sigue sin juegos | Pendiente |
| 4 | Se aceptan los permisos de las tres APIs de juegos y se rechazan combinaciones ajenas | `GetAccess_AceptaLosPermisosDeLasTresApisDeJuegosYRechazaCombinacionesAjenas`, `GetAccess_SoloMemoryEnLaApiDeTyping_NoHabilitaMemory` | Revisar en Auth0 que cada permiso de juego esté en la API de su juego | Pendiente |
| 5 | Usuario 1: ningún juego, y el acceso directo por URL queda bloqueado en el Shell | `GetAccess_Usuario1SinPermisos_Responde200ConListasVacias` | Entrar con el usuario 1, no ver juegos y abrir a mano la URL de un juego: el Shell lo bloquea | Pendiente |
| 6 | Usuario 2: solo Typing | `GetAccess_Usuario2_RespondeSoloTyping` | Entrar con el usuario 2 y ver solo Typing Battle | Pendiente |
| 7 | Usuario 3: los tres juegos | `GetAccess_Usuario3_RespondeLosTresJuegos` | Entrar con el usuario 3 y ver los tres juegos | Pendiente |
| 8 | Un permiso retirado se refleja al actualizar el Shell, sin que `/sync` lo restaure | `PermisoRetiradoEnAuth0_SeReflejaYNoReapareceAlVolverASincronizar` | Quitar `games.trivia.play` al usuario 3 en Auth0, esperar hasta un minuto (caché del Shell) y recargar: Trivia desaparece | Pendiente |
| 9 | Un fallo de Auth0 da un error controlado y no concede nada | `GetAccess_FalloDeAuth0_Responde503ProblemDetailsSinConcederNada`, `EndpointsAnteriores_FalloDeAuth0_Responden503` | Levantar con un `AUTH0_MGMT_CLIENT_SECRET` incorrecto: `/me/access` responde 503 con ProblemDetails y el Shell no muestra juegos | Pendiente |
| 10 | Dos cuentas conservan identidades separadas | `DosCuentas_ConservanIdentidadesYAccesosSeparados` | Entrar con el usuario 2 y luego con el 3 en otra ventana privada: cada uno ve su nombre y sus juegos | Pendiente |
| 11 | Los endpoints anteriores siguen funcionando con la misma fuente de Auth0 | `EndpointsAnteriores_LeenLosPermisosDeAuth0` | `curl` a `/me/permissions` y `/me/games` con el token del usuario 2 coincide con `/me/access` | Pendiente |
| 12 | El PR y los resultados de las pruebas se comparten sin secretos | El CI corre sin secretos (`.env.example`) | Revisar el PR y las capturas antes de publicarlos | Pendiente |

## Notas para la demostración

- El Shell guarda los permisos en caché hasta un minuto y no revoca al instante en pantallas ya abiertas. En la demostración no hay que prometer una revocación inmediata.
- Los usuarios 2 y 3 necesitan `matches.create`. Mientras no se decida en qué API de Auth0 vive (decisión pendiente de Oscar con el Equipo 3), el servicio lo acepta desde la API de Profile o la de Matchmaking.
