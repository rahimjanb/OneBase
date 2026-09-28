FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props OneBase.slnx ./
COPY src/ src/
RUN dotnet restore src/OneBase.Api/OneBase.Api.csproj
RUN dotnet publish src/OneBase.Api/OneBase.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OneBase.Api.dll"]
