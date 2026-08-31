# Etapa de build para los integration tests de LogiMatch.
# El contexto debe ser la raíz del repositorio para incluir las referencias de proyectos:
#   docker build -f src/IntegrationTests/Dockerfile -t logimatch-integrationtests .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS tests
WORKDIR /src

# Copiar los .csproj del proyecto de tests y sus dependencias (API, Application, Domain, Infrastructure).
COPY ["src/IntegrationTests/IntegrationTests.csproj", "IntegrationTests/"]
COPY ["src/API/API.csproj", "API/"]
COPY ["src/Application/Application.csproj", "Application/"]
COPY ["src/Domain/Domain.csproj", "Domain/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "IntegrationTests/IntegrationTests.csproj"

# Copiar todo el código fuente.
COPY src/ .

# Ejecutar los integration tests con xUnit.
ENTRYPOINT ["dotnet", "test", "--logger:console;verbosity=normal", "IntegrationTests/IntegrationTests.csproj"]
