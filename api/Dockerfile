FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Relifes.Api/Relifes.Api.csproj Relifes.Api/
RUN dotnet restore Relifes.Api/Relifes.Api.csproj
COPY Relifes.Api/ Relifes.Api/
RUN dotnet publish Relifes.Api/Relifes.Api.csproj --configuration Release --no-restore --output /app

# Debian-based on purpose: it ships the time zone database that grouping by local date relies on.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Relifes.Api.dll"]
