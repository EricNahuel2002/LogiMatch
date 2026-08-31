# Etapa de build para los unit tests de LogiMatch.
# El contexto debe ser la raíz del repositorio para incluir las referencias de proyectos:
#   docker build -f src/UnitTests/Dockerfile -t logimatch-unittests .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS tests
WORKDIR /src

# Copiar los .csproj del proyecto de tests y sus dependencias (API, Application, Domain, Infrastructure).
COPY ["src/UnitTests/UnitTests.csproj", "UnitTests/"]
COPY ["src/API/API.csproj", "API/"]
COPY ["src/Application/Application.csproj", "Application/"]
COPY ["src/Domain/Domain.csproj", "Domain/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "UnitTests/UnitTests.csproj"

# Copiar todo el código fuente.
COPY src/ .

# Ejecutar los tests con xUnit.
ENTRYPOINT ["dotnet", "test", "--logger:console;verbosity=normal", "UnitTests/UnitTests.csproj"]
