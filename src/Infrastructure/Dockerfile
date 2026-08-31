# Stage de migraciones EF Core para LogiMatch.
# Debe construirse con context en la raíz del repositorio para que las rutas src/... existan:
#   docker build -f src/Infrastructure/Dockerfile -t logimatch-migrations .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS migrations
WORKDIR /src

# Copiar los .csproj de Infrastructure y sus referencias para restaurar.
COPY ["src/Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["src/Domain/Domain.csproj", "Domain/"]
RUN dotnet restore "Infrastructure/Infrastructure.csproj"

# Copiar el resto del código fuente.
COPY src/ .

# Instalar herramientas EF Core para migraciones.
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# Compilar la solución para validar y habilitar las migraciones.
RUN dotnet build "Infrastructure/Infrastructure.csproj" -c Release

# Ejecutar migraciones contra la base de datos.
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "Infrastructure/Infrastructure.csproj"]
