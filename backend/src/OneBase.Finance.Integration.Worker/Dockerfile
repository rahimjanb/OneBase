# Служба интеграции 1С → OneBase (сервис onec-worker в docker-compose.yml). Сборка из каталога backend:
#   docker compose --profile onec build onec-worker
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/OneBase.Finance.Integration.Contracts/ src/OneBase.Finance.Integration.Contracts/
COPY src/OneBase.Finance.Integration.Worker/ src/OneBase.Finance.Integration.Worker/
RUN dotnet restore src/OneBase.Finance.Integration.Worker/OneBase.Finance.Integration.Worker.csproj
RUN dotnet publish src/OneBase.Finance.Integration.Worker/OneBase.Finance.Integration.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# /health — только внутри контейнера (порт наружу не публикуется).
ENV ASPNETCORE_HTTP_PORTS=8081 \
    DOTNET_ENVIRONMENT=Production
EXPOSE 8081
# Без root: в образе aspnet есть пользователь app.
USER $APP_UID
ENTRYPOINT ["dotnet", "OneBase.Finance.Integration.Worker.dll"]
