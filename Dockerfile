# Multi-stage build of the Api. Contains no project or company name: the
# assembly to start comes from the APP_ASSEMBLY build argument (.env.example).
# Images pinned and verified on mcr.microsoft.com (SDK matches global.json).

FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
ARG APP_ASSEMBLY
WORKDIR /source

# Central build settings first (analyzers, CPM, Fody configuration).
COPY global.json nuget.config .editorconfig Directory.Build.props Directory.Build.targets Directory.Packages.props FodyWeavers.xml ./
COPY src/ src/

RUN dotnet restore src/Api/Api.csproj
RUN dotnet publish src/Api/Api.csproj -c Release --no-restore -o /app/publish \
    && test -n "$APP_ASSEMBLY" \
    && test -f "/app/publish/$APP_ASSEMBLY.dll"

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS final
ARG APP_ASSEMBLY
ENV APP_ASSEMBLY=${APP_ASSEMBLY}
WORKDIR /app
COPY --from=build /app/publish .

# Non-root: the .NET 8+ images ship the "app" user; APP_UID=1654 (verified in this image).
# Source: https://devblogs.microsoft.com/dotnet/securing-containers-with-rootless/
USER $APP_UID

# Default ASP.NET Core port in .NET 8+ images (ASPNETCORE_HTTP_PORTS=8080).
# Source: https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/8.0/aspnet-port
EXPOSE 8080

# Exec form; `exec` replaces the shell so dotnet is PID 1 and receives SIGTERM.
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet \"$APP_ASSEMBLY.dll\""]
