# Multi-stage build para la API de LogiMatch.
# El contexto debe ser la raíz del repositorio para incluir las referencias de proyectos:
#   docker build -f src/API/Dockerfile -t logimatch-api .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar los .csproj de API y sus dependencias (Application, Domain) para restaurar.
COPY ["src/API/API.csproj", "API/"]
COPY ["src/Application/Application.csproj", "Application/"]
COPY ["src/Domain/Domain.csproj", "Domain/"]
RUN dotnet restore "API/API.csproj"

# Copiar el resto del código fuente y publicar.
COPY src/ .
RUN dotnet publish "API/API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "API.dll"]
