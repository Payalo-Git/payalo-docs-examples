# Multi-stage build: SDK image compiles, slim runtime image runs.
# We deliberately do NOT bake any secrets in — the API key comes from environment
# variables (or a secrets store) at runtime.

ARG DOTNET_VERSION=9.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Build context is samples/dotnet/ (see ../docker-compose.yml). Copy csproj first so
# layer caching reuses `restore` when only source files change.
COPY PayAlo.Sample.Web/PayAlo.Sample.Web.csproj PayAlo.Sample.Web/
RUN dotnet restore PayAlo.Sample.Web/PayAlo.Sample.Web.csproj

COPY . .
RUN dotnet publish PayAlo.Sample.Web/PayAlo.Sample.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

# Run as a non-root user. The aspnet image already has $APP_UID for this purpose.
USER $APP_UID

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "PayAlo.Sample.Web.dll"]
