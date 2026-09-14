# Multi-stage build for the web app. Build context is the repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached between source changes.
COPY src/PropertyManagement.Domain/PropertyManagement.Domain.csproj src/PropertyManagement.Domain/
COPY src/PropertyManagement.Application/PropertyManagement.Application.csproj src/PropertyManagement.Application/
COPY src/PropertyManagement.Infrastructure/PropertyManagement.Infrastructure.csproj src/PropertyManagement.Infrastructure/
COPY src/PropertyManagement.Web/PropertyManagement.Web.csproj src/PropertyManagement.Web/
RUN dotnet restore src/PropertyManagement.Web/PropertyManagement.Web.csproj

COPY src/ src/
RUN dotnet publish src/PropertyManagement.Web/PropertyManagement.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "PropertyManagement.Web.dll"]
