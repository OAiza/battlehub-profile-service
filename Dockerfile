# Imagen del Profile Service: una etapa para compilar y otra mínima para ejecutar.
# Versiones fijas de las imágenes base (sin "latest").

FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src

COPY global.json ./
COPY src/BattleHub.ProfileService.Api/BattleHub.ProfileService.Api.csproj src/BattleHub.ProfileService.Api/
RUN dotnet restore src/BattleHub.ProfileService.Api/BattleHub.ProfileService.Api.csproj

COPY src/ src/
RUN dotnet publish src/BattleHub.ProfileService.Api/BattleHub.ProfileService.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS runtime

# curl se usa para el healthcheck del contenedor.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# La imagen oficial de .NET trae un usuario sin privilegios: la API no corre como root.
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "BattleHub.ProfileService.Api.dll"]
