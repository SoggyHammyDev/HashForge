# HashForge pool: Stratum server, background jobs and web UI in one process.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore and publish in one step, with all sources present. Restoring from project files alone
# leaves the Blazor framework scripts out of the published output.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
RUN dotnet publish src/HashForge.Web/HashForge.Web.csproj --configuration Release --output /app \
    && test -f /app/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./

# The base image provides this unprivileged user.
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HashForge.Web.dll"]
